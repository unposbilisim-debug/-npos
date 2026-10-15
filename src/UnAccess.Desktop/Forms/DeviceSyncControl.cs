using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Sync;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class DeviceSyncControl : ScreenControl
{
    private readonly ComboBox _controller = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _status = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _operation = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Label _pending = new() { AutoSize = true, Margin = new Padding(0, 8, 16, 0) };
    private readonly Label _processing = new() { AutoSize = true, Margin = new Padding(0, 8, 16, 0) };
    private readonly Label _failed = new() { AutoSize = true, Margin = new Padding(0, 8, 16, 0) };
    private readonly Label _completed = new() { AutoSize = true, Margin = new Padding(0, 8, 16, 0) };
    private readonly DataGridView _grid = Grids.Create();

    public DeviceSyncControl(IServiceScopeFactory scopes, ILogger<DeviceSyncControl> logger)
        : base(scopes, logger)
    {
        _status.Items.AddRange(["Tümü", "Pending", "Processing", "Failed", "DeadLetter", "Completed"]);
        _status.SelectedIndex = 0;
        _operation.Items.AddRange(["Tümü", DeviceSyncOperations.UpsertCard, DeviceSyncOperations.RemoveCard, DeviceSyncOperations.SyncCardPermissions, DeviceSyncOperations.FullControllerSync]);
        _operation.SelectedIndex = 0;

        _grid.Columns.Add(Grids.Text(nameof(Row.Created), "Oluşturma", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Controller), "Kontrolör", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.Operation), "İşlem", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.Entity), "Varlık", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 110));
        _grid.Columns.Add(Grids.Text(nameof(Row.Attempts), "Deneme", 70));
        _grid.Columns.Add(Grids.Text(nameof(Row.LastAttempt), "Son Deneme", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Error), "Hata", 220));

        var summary = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, WrapContents = false };
        summary.Controls.AddRange([_pending, _processing, _failed, _completed]);

        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, WrapContents = false };
        filters.Controls.Add(new Label { Text = "Kontrolör", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        filters.Controls.Add(_controller);
        filters.Controls.Add(new Label { Text = "Durum", AutoSize = true, Margin = new Padding(12, 8, 6, 0) });
        filters.Controls.Add(_status);
        filters.Controls.Add(new Label { Text = "İşlem", AutoSize = true, Margin = new Padding(12, 8, 6, 0) });
        filters.Controls.Add(_operation);

        var refresh = ListButtons.Command("Yenile");
        var retry = ListButtons.Command("Başarısızları Tekrar Dene");
        retry.Width = 190;
        var full = ListButtons.Command("Kontrolörü Tam Senkronize Et");
        full.Width = 210;
        refresh.Click += async (_, _) => await ReloadAsync();
        retry.Click += async (_, _) => await RetryAsync();
        full.Click += async (_, _) => await FullSyncAsync();

        Controls.Add(_grid);
        Controls.Add(ListButtons.Bar(refresh, retry, full));
        Controls.Add(filters);
        Controls.Add(summary);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<DeviceSyncJobItem>? rows = null;
        DeviceSyncSummary? summary = null;
        IReadOnlyList<ControllerListItem>? controllers = null;
        var ok = await RunAsync(async services =>
        {
            var sync = services.GetService<IDeviceSyncService>() ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            var query = services.GetService<IControllerQueryService>() ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            controllers = await query.ListAsync(Cancellation);
            summary = await sync.GetSummaryAsync(Cancellation);
            rows = await sync.SearchAsync(Filter(), Cancellation);
        });
        if (!ok || rows == null || summary == null || controllers == null || IsDisposed)
            return;

        var selected = _controller.SelectedValue is int id ? id : 0;
        _controller.DisplayMember = nameof(LookupItem.Display);
        _controller.ValueMember = nameof(LookupItem.Id);
        _controller.DataSource = controllers
            .Select(item => new LookupItem { Id = item.Id, Name = item.Name })
            .Prepend(new LookupItem { Id = 0, Name = "Tümü" })
            .ToList();
        _controller.SelectedValue = selected;

        _pending.Text = "Bekleyen: " + summary.Pending;
        _processing.Text = "İşleniyor: " + summary.Processing;
        _failed.Text = "Başarısız: " + summary.Failed;
        _completed.Text = "Tamamlanan: " + summary.Completed;
        _grid.DataSource = rows.Select(row => new Row
        {
            Created = row.CreatedAtText,
            Controller = row.ControllerName,
            Operation = row.OperationText,
            Entity = row.EntityText,
            Status = row.StatusText,
            Attempts = row.AttemptCount.ToString(),
            LastAttempt = row.LastAttemptText,
            Error = row.LastError ?? ""
        }).ToList();
        SetStatus($"{rows.Count} senkronizasyon işi");
    }

    private async Task RetryAsync()
    {
        var ok = await RunAsync(async services =>
        {
            var sync = services.GetRequiredService<IDeviceSyncService>();
            var count = await sync.RetryFailedAsync(Cancellation);
            SetStatus(count == 0 ? "Yeniden denenecek iş yok." : $"{count} iş yeniden denemeye alındı.");
        });
        if (ok)
            await ReloadAsync();
    }

    private async Task FullSyncAsync()
    {
        if (_controller.SelectedValue is not int controllerId || controllerId <= 0)
        {
            MessageBox.Show(this, "Tam senkronizasyon için kontrolör seçiniz.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var ok = await RunAsync(async services =>
        {
            await services.GetRequiredService<IDeviceSyncService>().RequestFullSyncAsync(controllerId, Cancellation);
        });
        if (ok)
            await ReloadAsync();
    }

    private DeviceSyncFilter Filter() => new()
    {
        ControllerId = _controller.SelectedValue is int controllerId && controllerId > 0 ? controllerId : null,
        Status = _status.SelectedItem as string == "Tümü" ? null : _status.SelectedItem as string,
        OperationType = _operation.SelectedItem as string == "Tümü" ? null : _operation.SelectedItem as string
    };

    private sealed class Row
    {
        public string Created { get; init; } = "";
        public string Controller { get; init; } = "";
        public string Operation { get; init; } = "";
        public string Entity { get; init; } = "";
        public string Status { get; init; } = "";
        public string Attempts { get; init; } = "";
        public string LastAttempt { get; init; } = "";
        public string Error { get; init; } = "";
    }
}
