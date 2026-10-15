using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class FloorListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();
    private readonly ComboBox _building = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _edit = ListButtons.Command("Düzenle");
    private readonly Button _toggle = ListButtons.Command("Pasife Al");
    private bool _ready;

    public FloorListControl(IServiceScopeFactory scopes, ILogger<FloorListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.BuildingName), "Bina", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Code), "Kod", 70));
        _grid.Columns.Add(Grids.Text(nameof(Row.Name), "Kat", 160));
        _grid.Columns.Add(Grids.Text(nameof(Row.FloorNumber), "Kat No", 70));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 80));
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += async (_, args) =>
        {
            if (args.RowIndex >= 0)
                await EditAsync(Selected());
        };
        _building.Margin = new Padding(0, 4, 8, 8);
        _building.SelectedIndexChanged += async (_, _) =>
        {
            if (_ready)
                await ReloadAsync();
        };
        _edit.Click += async (_, _) => await EditAsync(Selected());
        _toggle.Click += async (_, _) => await ToggleAsync();
        var create = ListButtons.Command("+ Yeni");
        create.Click += async (_, _) => await EditAsync(null);

        var bar = ListButtons.Bar();
        bar.Controls.Add(new Label { Text = "Bina", AutoSize = true, Margin = new Padding(0, 10, 6, 0) });
        bar.Controls.Add(_building);
        bar.Controls.Add(create);
        bar.Controls.Add(_edit);
        bar.Controls.Add(_toggle);
        Controls.Add(_grid);
        Controls.Add(bar);
        UpdateButtons();
        Load += async (_, _) =>
        {
            await LoadBuildingsAsync();
            _ready = true;
            await ReloadAsync();
        };
    }

    private async Task LoadBuildingsAsync()
    {
        IReadOnlyList<LookupItem>? buildings = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            buildings = await service.GetBuildingOptionsAsync(Cancellation);
        });
        if (!ok || buildings == null || IsDisposed)
            return;

        var items = new List<LookupItem> { new() { Id = 0, Name = "Tümü" } };
        items.AddRange(buildings);
        _building.DisplayMember = nameof(LookupItem.Display);
        _building.ValueMember = nameof(LookupItem.Id);
        _building.DataSource = items;
    }

    private async Task ReloadAsync()
    {
        var buildingId = _building.SelectedValue is int id && id > 0 ? id : (int?)null;
        IReadOnlyList<FloorListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.ListFloorsAsync(buildingId, Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new Row
        {
            Id = row.Id,
            BuildingId = row.BuildingId,
            BuildingName = row.BuildingName,
            Code = row.Code,
            Name = row.Name,
            FloorNumber = row.FloorNumber,
            IsActive = row.IsActive,
            Status = Catalog.ActiveText(row.IsActive)
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
        UpdateButtons();
    }

    private async Task EditAsync(Row? existing)
    {
        IReadOnlyList<LookupItem>? buildings = null;
        var loaded = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            buildings = await service.GetBuildingOptionsAsync(Cancellation);
        });
        if (!loaded || buildings == null || IsDisposed)
            return;

        FloorEditModel? model;
        using (var form = new FloorEditForm(
            buildings,
            existing?.BuildingId,
            existing?.Code,
            existing?.Name,
            existing?.FloorNumber ?? 0,
            existing?.IsActive ?? true))
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
                await service.CreateFloorAsync(model, Cancellation);
            else
                await service.UpdateFloorAsync(existing.Id, model, Cancellation);
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
            await service.UpdateFloorAsync(row.Id, new FloorEditModel
            {
                BuildingId = row.BuildingId,
                Code = row.Code,
                Name = row.Name,
                FloorNumber = row.FloorNumber,
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
        public int BuildingId { get; init; }
        public string BuildingName { get; init; } = "";
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public int FloorNumber { get; init; }
        public bool IsActive { get; init; }
        public string Status { get; init; } = "";
    }
}

internal sealed class FloorEditForm : Form
{
    private readonly ComboBox _building = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _code = new() { MaxLength = FieldLengths.Code };
    private readonly TextBox _name = new() { MaxLength = FieldLengths.Name };
    private readonly NumericUpDown _number = new() { Minimum = -20, Maximum = 200, Width = 80 };

    public FloorEditForm(
        IReadOnlyList<LookupItem> buildings,
        int? buildingId,
        string? code,
        string? name,
        int floorNumber,
        bool isActive)
    {
        FormChrome.Apply(this, buildingId == null ? "Yeni Kat" : "Kat", new Size(460, 260));
        var choices = buildings.Where(item => item.IsActive || item.Id == buildingId).ToList();
        _building.DisplayMember = nameof(LookupItem.Display);
        _building.ValueMember = nameof(LookupItem.Id);
        _building.DataSource = choices;
        if (buildingId is int selectedBuilding)
        {
            _building.SelectedValue = selectedBuilding;
            _code.Text = code ?? "";
            _name.Text = name ?? "";
            _number.Value = Math.Clamp(floorNumber, (int)_number.Minimum, (int)_number.Maximum);
            _active.Checked = isActive;
        }

        var fields = FormChrome.Fields(5);
        FormChrome.AddField(fields, 0, "Bina", _building);
        FormChrome.AddField(fields, 1, "Kod", _code);
        FormChrome.AddField(fields, 2, "Ad", _name);
        FormChrome.AddField(fields, 3, "Kat No", _number);
        fields.Controls.Add(_active, 1, 4);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true };
    public FloorEditModel? Model { get; private set; }

    private void SaveModel()
    {
        try
        {
            if (_building.SelectedValue is not int buildingId || buildingId <= 0)
                throw new UnAccessException("Bina seçiniz.");

            Model = new FloorEditModel
            {
                BuildingId = buildingId,
                Code = TextRules.Code(_code.Text, "Kat kodu"),
                Name = TextRules.Name(_name.Text, "Kat adı"),
                FloorNumber = (int)_number.Value,
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
