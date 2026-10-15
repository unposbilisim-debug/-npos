using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal enum ReportKind
{
    Passages,
    Denied,
    PersonnelAccess,
    DoorAccess
}

internal sealed class ReportControl : ScreenControl
{
    private readonly ReportKind _kind;
    private readonly DateTimePicker _from = new() { ShowCheckBox = true, Width = 110, Format = DateTimePickerFormat.Short, Checked = false };
    private readonly DateTimePicker _to = new() { ShowCheckBox = true, Width = 110, Format = DateTimePickerFormat.Short, Checked = false };
    private readonly ComboBox _personnel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _department = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _door = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _zone = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ComboBox _pageSize = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly DataGridView _grid = Grids.Create();
    private readonly Label _pageLabel = new() { AutoSize = true, Margin = new Padding(8, 8, 0, 0), Text = "Sayfa 1" };
    private int _page = 1;
    private int _total;

    public ReportControl(ReportKind kind, IServiceScopeFactory scopes, ILogger logger)
        : base(scopes, logger)
    {
        _kind = kind;
        _pageSize.Items.AddRange(["50", "100", "250"]);
        _pageSize.SelectedItem = "100";
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72 };
        if (kind is ReportKind.Passages or ReportKind.Denied)
        {
            filters.Controls.Add(Caption("Tarih"));
            filters.Controls.Add(_from);
            filters.Controls.Add(_to);
        }

        filters.Controls.Add(Caption("Personel"));
        filters.Controls.Add(_personnel);
        filters.Controls.Add(Caption("Departman"));
        filters.Controls.Add(_department);
        filters.Controls.Add(Caption("Kapı"));
        filters.Controls.Add(_door);
        filters.Controls.Add(Caption("Bölge"));
        filters.Controls.Add(_zone);
        filters.Controls.Add(Caption("Sayfa"));
        filters.Controls.Add(_pageSize);
        var refresh = ActionButton("Yenile", async () => { _page = 1; await ReloadAsync(); });
        var previous = ActionButton("Önceki", async () => { if (_page > 1) { _page--; await ReloadAsync(); } });
        var next = ActionButton("Sonraki", async () =>
        {
        var size = SelectedPageSize();
        if (_page * size < _total)
            {
                _page++;
                await ReloadAsync();
            }
        });
        var export = ActionButton("CSV Dışa Aktar", ExportAsync);
        filters.Controls.Add(refresh);
        filters.Controls.Add(previous);
        filters.Controls.Add(next);
        filters.Controls.Add(export);
        filters.Controls.Add(_pageLabel);
        BuildColumns();
        Controls.Add(_grid);
        Controls.Add(filters);
        Load += async (_, _) =>
        {
            await LoadFiltersAsync();
            await ReloadAsync();
        };
    }

    private void BuildColumns()
    {
        if (_kind is ReportKind.Passages or ReportKind.Denied)
        {
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.When), "Tarih/Saat", 130));
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.Personnel), "Personel", 140));
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.Department), "Departman", 120));
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.Card), "Kart", 100));
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.Door), "Kapı", 120));
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.Zone), "Bölge", 120));
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.Result), "Sonuç", 80));
            _grid.Columns.Add(Grids.Text(nameof(PassageRow.Reason), "Gerekçe", 160));
            return;
        }

        if (_kind == ReportKind.PersonnelAccess)
        {
            _grid.Columns.Add(Grids.Text(nameof(PersonRow.Number), "Sicil", 80));
            _grid.Columns.Add(Grids.Text(nameof(PersonRow.Name), "Personel", 140));
            _grid.Columns.Add(Grids.Text(nameof(PersonRow.Department), "Departman", 120));
            _grid.Columns.Add(Grids.Text(nameof(PersonRow.Kind), "Tür", 80));
            _grid.Columns.Add(Grids.Text(nameof(PersonRow.Target), "Hedef", 140));
            _grid.Columns.Add(Grids.Text(nameof(PersonRow.Schedule), "Zaman Planı", 120));
            _grid.Columns.Add(Grids.Text(nameof(PersonRow.Effect), "Etki", 80));
            return;
        }

        _grid.Columns.Add(Grids.Text(nameof(DoorRow.Door), "Kapı", 140));
        _grid.Columns.Add(Grids.Text(nameof(DoorRow.Zone), "Bölge", 120));
        _grid.Columns.Add(Grids.Text(nameof(DoorRow.Kind), "Tür", 110));
        _grid.Columns.Add(Grids.Text(nameof(DoorRow.Subject), "Kime", 140));
        _grid.Columns.Add(Grids.Text(nameof(DoorRow.Schedule), "Zaman Planı", 120));
        _grid.Columns.Add(Grids.Text(nameof(DoorRow.Effect), "Etki", 80));
    }

    private async Task LoadFiltersAsync()
    {
        var ok = await RunAsync(async services =>
        {
            var reports = services.GetRequiredService<IReportService>();
            Bind(_personnel, await reports.PersonnelOptionsAsync(Cancellation));
            Bind(_department, await reports.DepartmentOptionsAsync(Cancellation));
            Bind(_door, await reports.DoorOptionsAsync(Cancellation));
            Bind(_zone, await reports.ZoneOptionsAsync(Cancellation));
        });
        if (!ok)
            SetStatus("Filtreler yüklenemedi");
    }

    private async Task ReloadAsync()
    {
        var ok = await RunAsync(async services =>
        {
            var reports = services.GetRequiredService<IReportService>();
            var clock = services.GetRequiredService<IHospitalClock>();
            if (_kind is ReportKind.Passages or ReportKind.Denied)
            {
                var page = await reports.PassagesAsync(PassageQuery(clock), Cancellation);
                _total = page.TotalCount;
                _grid.DataSource = page.Items.Select(item => new PassageRow
                {
                    When = clock.ToHospitalLocal(DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc)).ToString("dd.MM.yyyy HH:mm:ss"),
                    Personnel = item.PersonnelName,
                    Department = item.DepartmentName,
                    Card = item.CardNumber,
                    Door = item.DoorName,
                    Zone = item.ZoneName,
                    Result = item.Decision == "Granted" ? "ONAYLANDI" : item.Decision == "Denied" ? "REDDEDİLDİ" : item.Decision,
                    Reason = item.Reason
                }).ToList();
            }
            else if (_kind == ReportKind.PersonnelAccess)
            {
                var page = await reports.PersonnelAccessAsync(AccessQuery(), Cancellation);
                _total = page.TotalCount;
                _grid.DataSource = page.Items.Select(item => new PersonRow
                {
                    Number = item.PersonnelNumber,
                    Name = item.PersonnelName,
                    Department = item.DepartmentName,
                    Kind = item.Kind,
                    Target = item.Target,
                    Schedule = item.Schedule,
                    Effect = item.Effect
                }).ToList();
            }
            else
            {
                var page = await reports.DoorAccessAsync(AccessQuery(), Cancellation);
                _total = page.TotalCount;
                _grid.DataSource = page.Items.Select(item => new DoorRow
                {
                    Door = item.DoorName,
                    Zone = item.ZoneName,
                    Kind = item.Kind,
                    Subject = item.Subject,
                    Schedule = item.Schedule,
                    Effect = item.Effect
                }).ToList();
            }
        });
        if (!ok || IsDisposed)
            return;

        _pageLabel.Text = $"Sayfa {_page}";
        SetStatus($"{_total} kayıt");
    }

    private async Task ExportAsync()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "unaccess-rapor.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var ok = await RunAsync(async services =>
        {
            var reports = services.GetRequiredService<IReportService>();
            var clock = services.GetRequiredService<IHospitalClock>();
            await using var stream = File.Create(dialog.FileName);
            if (_kind is ReportKind.Passages or ReportKind.Denied)
                await reports.WritePassagesCsvAsync(PassageQuery(clock), stream, Cancellation);
            else if (_kind == ReportKind.PersonnelAccess)
                await reports.WritePersonnelAccessCsvAsync(AccessQuery(), stream, Cancellation);
            else
                await reports.WriteDoorAccessCsvAsync(AccessQuery(), stream, Cancellation);
        });
        if (ok)
            SetStatus("CSV yazıldı");
    }

    private PassageReportQuery PassageQuery(IHospitalClock clock) => new()
    {
        FromUtc = _from.Checked ? clock.ToUtc(_from.Value.Date) : null,
        ToUtc = _to.Checked ? clock.ToUtc(_to.Value.Date.AddDays(1)) : null,
        PersonnelId = Id(_personnel),
        DepartmentId = Id(_department),
        DoorId = Id(_door),
        ZoneId = Id(_zone),
        DeniedOnly = _kind == ReportKind.Denied,
        Page = _page,
        PageSize = SelectedPageSize()
    };

    private AccessReportQuery AccessQuery() => new()
    {
        PersonnelId = Id(_personnel),
        DepartmentId = Id(_department),
        DoorId = Id(_door),
        ZoneId = Id(_zone),
        Page = _page,
        PageSize = SelectedPageSize()
    };

    private int SelectedPageSize() => int.TryParse(_pageSize.SelectedItem as string, out var size) ? ReportPaging.NormalizeSize(size) : ReportPaging.DefaultSize;

    private static int? Id(ComboBox combo) => combo.SelectedItem is LookupItem item && item.Id > 0 ? item.Id : null;

    private static void Bind(ComboBox combo, IReadOnlyList<LookupItem> items)
    {
        var list = new List<LookupItem> { new() { Id = 0, Name = "Tümü" } };
        list.AddRange(items);
        combo.DisplayMember = nameof(LookupItem.Display);
        combo.DataSource = list;
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(8, 8, 4, 0) };

    private static Button ActionButton(string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(8, 4, 0, 0) };
        button.Click += async (_, _) => await action();
        return button;
    }

    private sealed class PassageRow
    {
        public string When { get; init; } = "";
        public string Personnel { get; init; } = "";
        public string Department { get; init; } = "";
        public string Card { get; init; } = "";
        public string Door { get; init; } = "";
        public string Zone { get; init; } = "";
        public string Result { get; init; } = "";
        public string Reason { get; init; } = "";
    }

    private sealed class PersonRow
    {
        public string Number { get; init; } = "";
        public string Name { get; init; } = "";
        public string Department { get; init; } = "";
        public string Kind { get; init; } = "";
        public string Target { get; init; } = "";
        public string Schedule { get; init; } = "";
        public string Effect { get; init; } = "";
    }

    private sealed class DoorRow
    {
        public string Door { get; init; } = "";
        public string Zone { get; init; } = "";
        public string Kind { get; init; } = "";
        public string Subject { get; init; } = "";
        public string Schedule { get; init; } = "";
        public string Effect { get; init; } = "";
    }
}
