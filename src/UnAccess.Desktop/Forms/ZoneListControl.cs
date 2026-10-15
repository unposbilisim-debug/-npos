using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class ZoneListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();
    private readonly ComboBox _floor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly Button _edit = ListButtons.Command("Düzenle");
    private readonly Button _toggle = ListButtons.Command("Pasife Al");
    private bool _ready;

    public ZoneListControl(IServiceScopeFactory scopes, ILogger<ZoneListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.Location), "Konum", 180));
        _grid.Columns.Add(Grids.Text(nameof(Row.Code), "Kod", 70));
        _grid.Columns.Add(Grids.Text(nameof(Row.Name), "Bölge", 160));
        _grid.Columns.Add(Grids.Text(nameof(Row.Critical), "Kritik", 70));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 80));
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += async (_, args) =>
        {
            if (args.RowIndex >= 0)
                await EditAsync(Selected());
        };
        _floor.Margin = new Padding(0, 4, 8, 8);
        _floor.SelectedIndexChanged += async (_, _) =>
        {
            if (_ready)
                await ReloadAsync();
        };
        _edit.Click += async (_, _) => await EditAsync(Selected());
        _toggle.Click += async (_, _) => await ToggleAsync();
        var create = ListButtons.Command("+ Yeni");
        create.Click += async (_, _) => await EditAsync(null);
        var bar = ListButtons.Bar();
        bar.Controls.Add(new Label { Text = "Kat", AutoSize = true, Margin = new Padding(0, 10, 6, 0) });
        bar.Controls.Add(_floor);
        bar.Controls.Add(create);
        bar.Controls.Add(_edit);
        bar.Controls.Add(_toggle);
        Controls.Add(_grid);
        Controls.Add(bar);
        UpdateButtons();
        Load += async (_, _) =>
        {
            await LoadFloorsAsync();
            _ready = true;
            await ReloadAsync();
        };
    }

    private async Task LoadFloorsAsync()
    {
        IReadOnlyList<FloorOption>? floors = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            floors = await service.GetFloorOptionsAsync(null, Cancellation);
        });
        if (!ok || floors == null || IsDisposed)
            return;

        var items = new List<FloorOption> { new() { Id = 0, Display = "Tümü" } };
        items.AddRange(floors);
        _floor.DisplayMember = nameof(FloorOption.Display);
        _floor.ValueMember = nameof(FloorOption.Id);
        _floor.DataSource = items;
    }

    private async Task ReloadAsync()
    {
        var floorId = _floor.SelectedValue is int id && id > 0 ? id : (int?)null;
        IReadOnlyList<ZoneListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.ListZonesAsync(null, floorId, Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new Row
        {
            Id = row.Id,
            FloorId = row.FloorId,
            Location = row.Location,
            Code = row.Code,
            Name = row.Name,
            IsCritical = row.IsCritical,
            IsActive = row.IsActive,
            Critical = Catalog.YesNo(row.IsCritical),
            Status = Catalog.ActiveText(row.IsActive)
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
        UpdateButtons();
    }

    private async Task EditAsync(Row? existing)
    {
        IReadOnlyList<FloorOption>? floors = null;
        var loaded = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            floors = await service.GetFloorOptionsAsync(null, Cancellation);
        });
        if (!loaded || floors == null || IsDisposed)
            return;

        ZoneEditModel? model;
        using (var form = new ZoneEditForm(floors, existing?.FloorId, existing?.Code, existing?.Name, existing?.IsCritical ?? false, existing?.IsActive ?? true))
        {
            if (form.ShowDialog(FindForm()) != DialogResult.OK)
                return;
            model = form.Model;
        }

        if (model == null)
            return;
        var saved = await RunAsync(async services =>
        {
            var service = services.GetRequiredService<IHospitalStructureService>();
            if (existing == null)
                await service.CreateZoneAsync(model, Cancellation);
            else
                await service.UpdateZoneAsync(existing.Id, model, Cancellation);
        });
        if (saved)
            await ReloadAsync();
    }

    private async Task ToggleAsync()
    {
        var row = Selected();
        if (row == null || !ListButtons.ConfirmToggle(this, row.IsActive))
            return;

        var saved = await RunAsync(async services =>
        {
            var service = services.GetRequiredService<IHospitalStructureService>();
            await service.UpdateZoneAsync(row.Id, new ZoneEditModel
            {
                FloorId = row.FloorId,
                Code = row.Code,
                Name = row.Name,
                IsCritical = row.IsCritical,
                IsActive = !row.IsActive
            }, Cancellation);
        });
        if (saved)
            await ReloadAsync();
    }

    private void UpdateButtons()
    {
        var row = Selected();
        _edit.Enabled = row != null;
        _toggle.Enabled = row != null;
        _toggle.Text = row is { IsActive: false } ? "Aktifleştir" : "Pasife Al";
    }

    private Row? Selected() => _grid.CurrentRow?.DataBoundItem as Row;

    private sealed class Row
    {
        public int Id { get; init; }
        public int FloorId { get; init; }
        public string Location { get; init; } = "";
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsCritical { get; init; }
        public bool IsActive { get; init; }
        public string Critical { get; init; } = "";
        public string Status { get; init; } = "";
    }
}

internal sealed class ZoneEditForm : Form
{
    private readonly ComboBox _floor = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _code = new() { MaxLength = FieldLengths.Code };
    private readonly TextBox _name = new() { MaxLength = FieldLengths.Name };
    private readonly CheckBox _critical = new() { Text = "Kritik bölge" };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true };

    public ZoneEditForm(IReadOnlyList<FloorOption> floors, int? floorId, string? code, string? name, bool isCritical, bool isActive)
    {
        FormChrome.Apply(this, floorId == null ? "Yeni Bölge" : "Bölge", new Size(520, 280));
        var choices = floors.Where(item => item.IsActive || item.Id == floorId).ToList();
        _floor.DisplayMember = nameof(FloorOption.Display);
        _floor.ValueMember = nameof(FloorOption.Id);
        _floor.DataSource = choices;
        if (floorId is int selected)
        {
            _floor.SelectedValue = selected;
            _code.Text = code ?? "";
            _name.Text = name ?? "";
            _critical.Checked = isCritical;
            _active.Checked = isActive;
        }

        var fields = FormChrome.Fields(5);
        FormChrome.AddField(fields, 0, "Kat", _floor);
        FormChrome.AddField(fields, 1, "Kod", _code);
        FormChrome.AddField(fields, 2, "Ad", _name);
        fields.Controls.Add(_critical, 1, 3);
        fields.Controls.Add(_active, 1, 4);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public ZoneEditModel? Model { get; private set; }

    private void SaveModel()
    {
        try
        {
            if (_floor.SelectedValue is not int floorId || floorId <= 0)
                throw new UnAccessException("Kat seçiniz.");

            Model = new ZoneEditModel
            {
                FloorId = floorId,
                Code = TextRules.Code(_code.Text, "Bölge kodu"),
                Name = TextRules.Name(_name.Text, "Bölge adı"),
                IsCritical = _critical.Checked,
                IsActive = _active.Checked
            };
            DialogResult = DialogResult.OK;
        }
        catch (UnAccessException exception)
        {
            MessageBox.Show(this, exception.UserMessage, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
