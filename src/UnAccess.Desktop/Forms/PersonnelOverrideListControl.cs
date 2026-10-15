using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class PersonnelOverrideListControl : ScreenControl
{
    private readonly ComboBox _personnel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly DataGridView _grid = Grids.Create();
    private List<PersonnelOption> _people = [];

    public PersonnelOverrideListControl(IServiceScopeFactory scopes, ILogger<PersonnelOverrideListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.Target), "Hedef", 180));
        _grid.Columns.Add(Grids.Text(nameof(Row.Result), "Sonuç", 80));
        _grid.Columns.Add(Grids.Text(nameof(Row.Schedule), "Zaman planı", 120));
        _grid.Columns.Add(Grids.Text(nameof(Row.ValidFrom), "Başlangıç", 90));
        _grid.Columns.Add(Grids.Text(nameof(Row.ValidUntil), "Bitiş", 90));
        _grid.Columns.Add(Grids.Text(nameof(Row.Reason), "Gerekçe", 180));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 70));
        var create = ListButtons.Command("+ Yeni");
        var edit = ListButtons.Command("Düzenle");
        create.Click += async (_, _) => await EditAsync(null);
        edit.Click += async (_, _) => await EditAsync(Selected());
        _personnel.SelectedIndexChanged += async (_, _) => await LoadOverridesAsync();

        var bar = ListButtons.Bar(create, edit);
        bar.Controls.Add(new Label { Text = "Personel", AutoSize = true, Margin = new Padding(12, 10, 6, 0) });
        _personnel.Margin = new Padding(0, 6, 0, 0);
        bar.Controls.Add(_personnel);
        Controls.Add(_grid);
        Controls.Add(bar);
        Load += async (_, _) => await ReloadPeopleAsync();
    }

    private async Task ReloadPeopleAsync()
    {
        IReadOnlyList<PersonnelListItem>? people = null;
        var ok = await RunAsync(async services =>
        {
            people = await services.GetRequiredService<IPersonnelService>().SearchAsync(new PersonnelSearchCriteria(), Cancellation);
        });
        if (!ok || people == null || IsDisposed)
            return;

        _people = people.Select(item => new PersonnelOption
        {
            Id = item.Id,
            PersonnelNumber = item.PersonnelNumber,
            FullName = item.FirstName + " " + item.LastName
        }).ToList();
        _personnel.DisplayMember = nameof(PersonnelOption.Display);
        _personnel.ValueMember = nameof(PersonnelOption.Id);
        _personnel.DataSource = _people;
    }

    private async Task LoadOverridesAsync()
    {
        if (_personnel.SelectedItem is not PersonnelOption person)
            return;

        PersonnelAuthorizationView? view = null;
        var ok = await RunAsync(async services =>
        {
            view = await services.GetRequiredService<IPersonnelAuthorizationService>().GetAsync(person.Id, Cancellation);
        });
        if (!ok || view == null || IsDisposed)
            return;

        _grid.DataSource = view.Overrides.Select(item => new Row
        {
            Model = item,
            Target = item.TargetName,
            Result = item.ResultText,
            Schedule = item.ScheduleName,
            ValidFrom = Catalog.Date(item.ValidFrom),
            ValidUntil = Catalog.Date(item.ValidUntil),
            Reason = item.Reason ?? "",
            Status = Catalog.ActiveText(item.IsActive)
        }).ToList();
        SetStatus($"{view.Overrides.Count} istisna");
    }

    private async Task EditAsync(Row? existing)
    {
        if (_personnel.SelectedItem is not PersonnelOption person)
            return;

        IReadOnlyList<ZoneOption>? zones = null;
        IReadOnlyList<DoorListItem>? doors = null;
        IReadOnlyList<TimeScheduleListItem>? schedules = null;
        var loaded = await RunAsync(async services =>
        {
            zones = await services.GetRequiredService<IHospitalStructureService>().GetZoneOptionsAsync(Cancellation);
            doors = await services.GetRequiredService<IHospitalStructureService>().ListDoorsAsync(null, Cancellation);
            schedules = await services.GetRequiredService<ITimeScheduleService>().ListAsync(Cancellation);
        });
        if (!loaded || zones == null || doors == null || schedules == null || IsDisposed)
            return;

        PersonnelOverrideEditModel? model;
        using (var form = new PersonnelOverrideEditForm(person.Id, existing?.Model, zones, doors, schedules))
        {
            if (form.ShowDialog(FindForm()) != DialogResult.OK)
                return;
            model = form.Model;
        }

        if (model == null)
            return;
        var saved = await RunAsync(async services =>
        {
            await services.GetRequiredService<IPersonnelAuthorizationService>().SaveOverrideAsync(model, Cancellation);
        });
        if (saved)
            await LoadOverridesAsync();
    }

    private Row? Selected() => _grid.CurrentRow?.DataBoundItem as Row;

    private sealed class Row
    {
        public PersonnelOverrideItem Model { get; init; } = null!;
        public string Target { get; init; } = "";
        public string Result { get; init; } = "";
        public string Schedule { get; init; } = "";
        public string ValidFrom { get; init; } = "";
        public string ValidUntil { get; init; } = "";
        public string Reason { get; init; } = "";
        public string Status { get; init; } = "";
    }
}

internal sealed class PersonnelOverrideEditForm : Form
{
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _target = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _schedule = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _allowed = new() { Text = "İzin ver", Checked = true, AutoSize = true };
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _until = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true };
    private readonly TextBox _reason = new() { MaxLength = FieldLengths.Description };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true, AutoSize = true };
    private readonly int _personnelId;
    private readonly int _id;
    private readonly IReadOnlyList<ZoneOption> _zones;
    private readonly IReadOnlyList<DoorListItem> _doors;

    public PersonnelOverrideEditForm(
        int personnelId,
        PersonnelOverrideItem? existing,
        IReadOnlyList<ZoneOption> zones,
        IReadOnlyList<DoorListItem> doors,
        IReadOnlyList<TimeScheduleListItem> schedules)
    {
        _personnelId = personnelId;
        _id = existing?.Id ?? 0;
        _zones = zones;
        _doors = doors;
        FormChrome.Apply(this, existing == null ? "Yeni istisna" : "İstisna", new Size(480, 420));
        _kind.Items.AddRange(["Bölge", "Kapı"]);
        _kind.SelectedIndex = existing?.DoorId != null ? 1 : 0;
        _kind.SelectedIndexChanged += (_, _) => BindTargets(existing);
        BindTargets(existing);
        _schedule.DisplayMember = nameof(TimeScheduleListItem.Name);
        _schedule.ValueMember = nameof(TimeScheduleListItem.Id);
        _schedule.DataSource = schedules.Where(item => item.IsActive || item.Id == existing?.TimeScheduleId).ToList();
        if (existing != null)
        {
            _schedule.SelectedValue = existing.TimeScheduleId;
            _allowed.Checked = existing.IsAllowed;
            _from.Value = existing.ValidFrom.ToDateTime(TimeOnly.MinValue);
            if (existing.ValidUntil is DateOnly until)
            {
                _until.Checked = true;
                _until.Value = until.ToDateTime(TimeOnly.MinValue);
            }
            _reason.Text = existing.Reason ?? "";
            _active.Checked = existing.IsActive;
        }

        var fields = FormChrome.Fields(7);
        FormChrome.AddField(fields, 0, "Hedef", _kind);
        FormChrome.AddField(fields, 1, "Seçim", _target);
        FormChrome.AddField(fields, 2, "Zaman planı", _schedule);
        fields.Controls.Add(_allowed, 1, 3);
        FormChrome.AddField(fields, 4, "Başlangıç", _from);
        FormChrome.AddField(fields, 5, "Bitiş", _until);
        FormChrome.AddField(fields, 6, "Gerekçe", _reason);
        fields.RowCount = 8;
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fields.Controls.Add(_active, 1, 7);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public PersonnelOverrideEditModel? Model { get; private set; }

    private void BindTargets(PersonnelOverrideItem? existing)
    {
        if (_kind.SelectedIndex == 1)
        {
            _target.DisplayMember = nameof(DoorListItem.Name);
            _target.ValueMember = nameof(DoorListItem.Id);
            _target.DataSource = _doors.ToList();
            if (existing?.DoorId is int doorId)
                _target.SelectedValue = doorId;
        }
        else
        {
            _target.DisplayMember = nameof(ZoneOption.Display);
            _target.ValueMember = nameof(ZoneOption.Id);
            _target.DataSource = _zones.ToList();
            if (existing?.ZoneId is int zoneId)
                _target.SelectedValue = zoneId;
        }
    }

    private void SaveModel()
    {
        try
        {
            if (_target.SelectedValue is not int targetId || targetId <= 0)
                throw new UnAccessException("Hedef seçiniz.");
            if (_schedule.SelectedValue is not int scheduleId || scheduleId <= 0)
                throw new UnAccessException("Zaman planı seçiniz.");

            var door = _kind.SelectedIndex == 1;
            Model = new PersonnelOverrideEditModel
            {
                Id = _id,
                PersonnelId = _personnelId,
                ZoneId = door ? null : targetId,
                DoorId = door ? targetId : null,
                TimeScheduleId = scheduleId,
                IsAllowed = _allowed.Checked,
                ValidFrom = DateOnly.FromDateTime(_from.Value),
                ValidUntil = _until.Checked ? DateOnly.FromDateTime(_until.Value) : null,
                Reason = _reason.Text,
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
