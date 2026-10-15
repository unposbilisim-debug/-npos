using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class ControllerListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();

    public ControllerListControl(IServiceScopeFactory scopes, ILogger<ControllerListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.Name), "Kontrolör", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.IpAddress), "IP", 110));
        _grid.Columns.Add(Grids.Text(nameof(Row.Model), "Model", 90));
        _grid.Columns.Add(Grids.Text(nameof(Row.Connection), "Durum", 90));
        _grid.Columns.Add(Grids.Text(nameof(Row.LastSeen), "Son Görülme", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.LastSync), "Son Senkronizasyon", 130));
        _grid.Columns.Add(Grids.Text(nameof(Row.Pending), "Bekleyen İş", 80));
        _grid.Columns.Add(Grids.Text(nameof(Row.LastError), "Son Hata", 160));
        Controls.Add(_grid);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<ControllerListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IControllerQueryService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.ListAsync(Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new Row
        {
            Name = row.Name,
            IpAddress = row.IpAddress,
            Model = row.Model,
            Connection = row.ConnectionText,
            LastSeen = row.LastSeenAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "",
            LastSync = row.LastSuccessfulSyncAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "",
            Pending = row.PendingJobs.ToString(),
            LastError = row.LastError ?? ""
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
    }

    private sealed class Row
    {
        public string Name { get; init; } = "";
        public string IpAddress { get; init; } = "";
        public string Model { get; init; } = "";
        public string Connection { get; init; } = "";
        public string LastSeen { get; init; } = "";
        public string LastSync { get; init; } = "";
        public string Pending { get; init; } = "";
        public string LastError { get; init; } = "";
    }
}
