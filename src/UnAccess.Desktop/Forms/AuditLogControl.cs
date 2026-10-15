using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class AuditLogControl : ScreenControl
{
    private readonly DateTimePicker _from = new() { ShowCheckBox = true, Width = 120, Format = DateTimePickerFormat.Short, Checked = false };
    private readonly DateTimePicker _to = new() { ShowCheckBox = true, Width = 120, Format = DateTimePickerFormat.Short, Checked = false };
    private readonly ComboBox _user = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly TextBox _action = new() { Width = 100 };
    private readonly TextBox _entity = new() { Width = 100 };
    private readonly TextBox _search = new() { Width = 140 };
    private readonly DataGridView _grid = Grids.Create();
    private int _page = 1;

    public AuditLogControl(IServiceScopeFactory scopes, ILogger<AuditLogControl> logger)
        : base(scopes, logger)
    {
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
        filters.Controls.Add(Label("Tarih"));
        filters.Controls.Add(_from);
        filters.Controls.Add(_to);
        filters.Controls.Add(Label("Kullanıcı"));
        filters.Controls.Add(_user);
        filters.Controls.Add(Label("İşlem"));
        filters.Controls.Add(_action);
        filters.Controls.Add(Label("Varlık"));
        filters.Controls.Add(_entity);
        filters.Controls.Add(Label("Arama"));
        filters.Controls.Add(_search);
        var search = new Button { Text = "Yenile", AutoSize = true, Height = 28 };
        search.Click += async (_, _) => { _page = 1; await ReloadAsync(); };
        filters.Controls.Add(search);
        _grid.Columns.Add(Grids.Text(nameof(Row.When), "Tarih/Saat", 130));
        _grid.Columns.Add(Grids.Text(nameof(Row.User), "Kullanıcı", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.Action), "İşlem", 90));
        _grid.Columns.Add(Grids.Text(nameof(Row.Entity), "Varlık", 110));
        _grid.Columns.Add(Grids.Text(nameof(Row.Description), "Açıklama", 220));
        _grid.Columns.Add(Grids.Text(nameof(Row.Computer), "Bilgisayar", 110));
        _grid.CellDoubleClick += async (_, _) => await OpenDetailAsync();
        Controls.Add(_grid);
        Controls.Add(filters);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        ReportPage<AuditLogRow>? page = null;
        IReadOnlyList<LookupItem>? users = null;
        var ok = await RunAsync(async services =>
        {
            var audit = services.GetRequiredService<IAuditQueryService>();
            var clock = services.GetRequiredService<IHospitalClock>();
            users = await audit.ListUsersAsync(Cancellation);
            page = await audit.SearchAsync(new AuditQuery
            {
                FromUtc = _from.Checked ? clock.ToUtc(_from.Value.Date) : null,
                ToUtc = _to.Checked ? clock.ToUtc(_to.Value.Date.AddDays(1)) : null,
                UserId = _user.SelectedItem is LookupItem user && user.Id > 0 ? user.Id : null,
                Action = _action.Text,
                EntityType = _entity.Text,
                Search = _search.Text,
                Page = _page,
                PageSize = ReportPaging.DefaultSize
            }, Cancellation);
        });
        if (!ok || page == null || users == null || IsDisposed)
            return;

        if (_user.Items.Count == 0)
        {
            _user.DisplayMember = nameof(LookupItem.Display);
            _user.Items.Add(new LookupItem { Id = 0, Name = "Tümü" });
            foreach (var user in users)
                _user.Items.Add(user);
            _user.SelectedIndex = 0;
        }

        _grid.DataSource = page.Items.Select(item => new Row
        {
            Id = item.Id,
            When = item.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),
            User = item.UserName,
            Action = item.Action,
            Entity = item.EntityType,
            Description = item.Description,
            Computer = item.ComputerName
        }).ToList();
        SetStatus($"{page.TotalCount} kayıt");
    }

    private async Task OpenDetailAsync()
    {
        if (_grid.CurrentRow?.DataBoundItem is not Row row)
            return;

        AuditLogDetail? detail = null;
        var ok = await RunAsync(async services =>
        {
            detail = await services.GetRequiredService<IAuditQueryService>().GetAsync(row.Id, Cancellation);
        });
        if (!ok || detail == null)
            return;

        using var form = new AuditDetailForm(detail);
        form.ShowDialog(this);
    }

    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(8, 8, 4, 0) };

    private sealed class Row
    {
        public int Id { get; init; }
        public string When { get; init; } = "";
        public string User { get; init; } = "";
        public string Action { get; init; } = "";
        public string Entity { get; init; } = "";
        public string Description { get; init; } = "";
        public string Computer { get; init; } = "";
    }
}

internal sealed class AuditDetailForm : Form
{
    public AuditDetailForm(AuditLogDetail detail)
    {
        FormChrome.Apply(this, "İşlem Ayrıntısı", new Size(640, 420));
        var oldValue = Box(detail.OldValue);
        var newValue = Box(detail.NewValue);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(12) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Eski Değer", AutoSize = true }, 0, 0);
        layout.Controls.Add(new Label { Text = "Yeni Değer", AutoSize = true }, 1, 0);
        layout.Controls.Add(oldValue, 0, 1);
        layout.Controls.Add(newValue, 1, 1);
        var close = new Button { Text = "Kapat", DialogResult = DialogResult.OK, Width = 96, Height = 28 };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44 };
        bar.Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        Controls.Add(layout);
        Controls.Add(bar);
    }

    private static TextBox Box(string? value) => new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        Text = value ?? "",
        WordWrap = false
    };
}
