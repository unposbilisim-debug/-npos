using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class DoorListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();
    private readonly ComboBox _zone = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly Button _edit = ListButtons.Command("Düzenle");
    private readonly Button _toggle = ListButtons.Command("Pasife Al");
    private bool _ready;

    public DoorListControl(IServiceScopeFactory scopes, ILogger<DoorListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.Location), "Konum", 220));
        _grid.Columns.Add(Grids.Text(nameof(Row.Name), "Kapı", 140));
        _grid.Columns.Add(Grids.Text(nameof(Row.Channel), "Kanal", 60));
        _grid.Columns.Add(Grids.Text(nameof(Row.ControllerName), "Kontrolör", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.Critical), "Kritik", 60));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 70));
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += async (_, args) =>
        {
            if (args.RowIndex >= 0)
                await EditAsync(Selected()?.Id);
        };
        _zone.Margin = new Padding(0, 4, 8, 8);
        _zone.SelectedIndexChanged += async (_, _) =>
        {
            if (_ready)
                await ReloadAsync();
        };
        _edit.Click += async (_, _) => await EditAsync(Selected()?.Id);
        _toggle.Click += async (_, _) => await ToggleAsync();
        var create = ListButtons.Command("+ Yeni");
        create.Click += async (_, _) => await EditAsync(null);
        var bar = ListButtons.Bar();
        bar.Controls.Add(new Label { Text = "Bölge", AutoSize = true, Margin = new Padding(0, 10, 6, 0) });
        bar.Controls.Add(_zone);
        bar.Controls.Add(create);
        bar.Controls.Add(_edit);
        bar.Controls.Add(_toggle);
        Controls.Add(_grid);
        Controls.Add(bar);
        UpdateButtons();
        Load += async (_, _) =>
        {
            await LoadZonesAsync();
            _ready = true;
            await ReloadAsync();
        };
    }

    private async Task LoadZonesAsync()
    {
        IReadOnlyList<ZoneOption>? zones = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            zones = await service.GetZoneOptionsAsync(Cancellation);
        });
        if (!ok || zones == null || IsDisposed)
            return;

        var items = new List<ZoneOption> { new() { Id = 0, Display = "Tümü" } };
        items.AddRange(zones);
        _zone.DisplayMember = nameof(ZoneOption.Display);
        _zone.ValueMember = nameof(ZoneOption.Id);
        _zone.DataSource = items;
    }

    private async Task ReloadAsync()
    {
        var zoneId = _zone.SelectedValue is int id && id > 0 ? id : (int?)null;
        IReadOnlyList<DoorListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.ListDoorsAsync(zoneId, Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new Row
        {
            Id = row.Id,
            Location = row.Location,
            Name = row.Name,
            Channel = row.ChannelNumber.ToString(),
            ControllerName = row.ControllerName ?? "",
            Critical = Catalog.YesNo(row.IsCritical),
            IsActive = row.IsActive,
            Status = Catalog.ActiveText(row.IsActive)
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
        UpdateButtons();
    }

    private async Task EditAsync(int? id)
    {
        IReadOnlyList<ZoneOption>? zones = null;
        IReadOnlyList<ControllerOption>? controllers = null;
        DoorEditModel? existing = null;
        var found = id == null;
        var loaded = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            zones = await service.GetZoneOptionsAsync(Cancellation);
            controllers = await service.GetControllerOptionsAsync(Cancellation);
            if (id is int doorId)
            {
                existing = await service.GetDoorAsync(doorId, Cancellation);
                found = existing != null;
            }
        });
        if (!loaded || zones == null || controllers == null || IsDisposed)
            return;
        if (!found)
        {
            MessageBox.Show(this, "Kapı bulunamadı.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await ReloadAsync();
            return;
        }

        DoorEditModel? model;
        using (var form = new DoorEditForm(zones, controllers, existing))
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
            if (id == null)
                await service.CreateDoorAsync(model, Cancellation);
            else
                await service.UpdateDoorAsync(id.Value, model, Cancellation);
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
            var model = await service.GetDoorAsync(row.Id, Cancellation)
                ?? throw new UnAccessException("Kapı bulunamadı.");
            model.IsActive = !model.IsActive;
            await service.UpdateDoorAsync(row.Id, model, Cancellation);
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
        public string Location { get; init; } = "";
        public string Name { get; init; } = "";
        public string Channel { get; init; } = "";
        public string ControllerName { get; init; } = "";
        public string Critical { get; init; } = "";
        public bool IsActive { get; init; }
        public string Status { get; init; } = "";
    }
}

internal sealed class DoorEditForm : Form
{
    private readonly ComboBox _zone = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _code = new() { MaxLength = FieldLengths.Code };
    private readonly TextBox _name = new() { MaxLength = FieldLengths.Name };
    private readonly ComboBox _controller = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _channel = new() { Minimum = 0, Maximum = 128 };
    private readonly TextBox _entry = new() { MaxLength = FieldLengths.ReaderName };
    private readonly CheckBox _hasExit = new() { Text = "Çıkış okuyucusu var" };
    private readonly TextBox _exit = new() { MaxLength = FieldLengths.ReaderName, Enabled = false };
    private readonly CheckBox _critical = new() { Text = "Kritik kapı" };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true };

    public DoorEditForm(
        IReadOnlyList<ZoneOption> zones,
        IReadOnlyList<ControllerOption> controllers,
        DoorEditModel? existing)
    {
        FormChrome.Apply(this, existing == null ? "Yeni Kapı" : "Kapı", new Size(560, 460));
        var zoneChoices = zones.Where(item => item.IsActive || item.Id == existing?.ZoneId).ToList();
        _zone.DisplayMember = nameof(ZoneOption.Display);
        _zone.ValueMember = nameof(ZoneOption.Id);
        _zone.DataSource = zoneChoices;

        var controllerChoices = new List<ControllerOption> { new() { Id = 0, Display = "(Yok)" } };
        controllerChoices.AddRange(controllers.Where(item => item.IsActive || item.Id == existing?.ControllerId));
        _controller.DisplayMember = nameof(ControllerOption.Display);
        _controller.ValueMember = nameof(ControllerOption.Id);
        _controller.DataSource = controllerChoices;
        _hasExit.CheckedChanged += (_, _) => _exit.Enabled = _hasExit.Checked;

        if (existing != null)
        {
            _zone.SelectedValue = existing.ZoneId;
            _code.Text = existing.Code;
            _name.Text = existing.Name;
            _controller.SelectedValue = existing.ControllerId ?? 0;
            _channel.Value = Math.Clamp(existing.ChannelNumber, 0, 128);
            _entry.Text = existing.EntryReaderName ?? "";
            _hasExit.Checked = existing.HasExitReader;
            _exit.Text = existing.ExitReaderName ?? "";
            _exit.Enabled = existing.HasExitReader;
            _critical.Checked = existing.IsCritical;
            _active.Checked = existing.IsActive;
        }

        var fields = FormChrome.Fields(10);
        FormChrome.AddField(fields, 0, "Bölge", _zone);
        FormChrome.AddField(fields, 1, "Kod", _code);
        FormChrome.AddField(fields, 2, "Ad", _name);
        FormChrome.AddField(fields, 3, "Kontrolör", _controller);
        FormChrome.AddField(fields, 4, "Kanal", _channel);
        FormChrome.AddField(fields, 5, "Giriş okuyucu", _entry);
        fields.Controls.Add(_hasExit, 1, 6);
        FormChrome.AddField(fields, 7, "Çıkış okuyucu", _exit);
        fields.Controls.Add(_critical, 1, 8);
        fields.Controls.Add(_active, 1, 9);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public DoorEditModel? Model { get; private set; }

    private void SaveModel()
    {
        try
        {
            if (_zone.SelectedValue is not int zoneId || zoneId <= 0)
                throw new UnAccessException("Bölge seçiniz.");

            var controllerId = _controller.SelectedValue is int id && id > 0 ? id : (int?)null;
            Model = new DoorEditModel
            {
                ZoneId = zoneId,
                Code = TextRules.Code(_code.Text, "Kapı kodu"),
                Name = TextRules.Name(_name.Text, "Kapı adı"),
                ControllerId = controllerId,
                ChannelNumber = (int)_channel.Value,
                EntryReaderName = _entry.Text,
                HasExitReader = _hasExit.Checked,
                ExitReaderName = _exit.Text,
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
