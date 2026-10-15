using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class PassageSimulatorControl : ScreenControl
{
    private readonly ComboBox _personnel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly ComboBox _door = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly Label _card = new() { AutoSize = true, Margin = new Padding(8, 10, 0, 0), Text = "Kart: -" };
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _time = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 100 };
    private readonly Label _result = new() { AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
    private readonly Label _reason = new() { AutoSize = true, MaximumSize = new Size(640, 0), Margin = new Padding(0, 8, 0, 0) };

    public PassageSimulatorControl(IServiceScopeFactory scopes, ILogger<PassageSimulatorControl> logger)
        : base(scopes, logger)
    {
        var read = new Button { Text = "KART OKUT", Width = 120, Height = 32, Margin = new Padding(12, 4, 0, 0) };
        _result.Font = new Font(Font, FontStyle.Bold);
        read.Click += async (_, _) => await ReadAsync();
        _personnel.SelectedIndexChanged += async (_, _) => await ShowCardAsync();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 120, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
        bar.Controls.Add(Caption("Personel"));
        bar.Controls.Add(_personnel);
        bar.Controls.Add(_card);
        bar.Controls.Add(Caption("Kapı"));
        bar.Controls.Add(_door);
        bar.Controls.Add(Caption("Tarih"));
        bar.Controls.Add(_date);
        bar.Controls.Add(_time);
        bar.Controls.Add(read);

        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8) };
        body.Controls.Add(_result);
        body.Controls.Add(_reason);
        Controls.Add(body);
        Controls.Add(bar);
        Load += async (_, _) => await LoadChoicesAsync();
    }

    private async Task LoadChoicesAsync()
    {
        IReadOnlyList<PersonnelListItem>? people = null;
        IReadOnlyList<DoorListItem>? doors = null;
        var ok = await RunAsync(async services =>
        {
            people = await services.GetRequiredService<IPersonnelService>().SearchAsync(new PersonnelSearchCriteria { IsActive = true }, Cancellation);
            doors = await services.GetRequiredService<IHospitalStructureService>().ListDoorsAsync(null, Cancellation);
        });
        if (!ok || people == null || doors == null || IsDisposed)
            return;

        _personnel.DisplayMember = nameof(PersonnelOption.Display);
        _personnel.ValueMember = nameof(PersonnelOption.Id);
        _personnel.DataSource = people.Select(item => new PersonnelOption
        {
            Id = item.Id,
            PersonnelNumber = item.PersonnelNumber,
            FullName = item.FirstName + " " + item.LastName
        }).ToList();
        _door.DisplayMember = nameof(DoorChoice.Display);
        _door.ValueMember = nameof(DoorChoice.Id);
        _door.DataSource = doors.Where(item => item.IsActive).Select(item => new DoorChoice(item.Id, $"{item.Location} / {item.Name}")).ToList();
    }

    private async Task ShowCardAsync()
    {
        if (_personnel.SelectedItem is not PersonnelOption person)
            return;

        PersonnelDetail? detail = null;
        var ok = await RunAsync(async services =>
        {
            detail = await services.GetRequiredService<IPersonnelService>().GetAsync(person.Id, Cancellation);
        });
        if (!ok || IsDisposed)
            return;

        var card = detail?.Cards.FirstOrDefault(item => item.Status == CardStatus.Active);
        _card.Text = card == null ? "Kart: aktif kart yok" : $"Kart: {card.CardNumber}";
    }

    private async Task ReadAsync()
    {
        if (_personnel.SelectedValue is not int personnelId || personnelId <= 0)
            return;
        if (_door.SelectedValue is not int doorId || doorId <= 0)
            return;

        var local = DateTime.SpecifyKind(_date.Value.Date.Add(_time.Value.TimeOfDay), DateTimeKind.Unspecified);
        SimulatedPassageResult? result = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<ISimulatedPassageService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            result = await service.ReadCardAsync(personnelId, doorId, local, Cancellation);
        });
        if (!ok || result == null || IsDisposed)
            return;

        var decision = result.Decision;
        _result.Text = decision.IsAllowed ? "GEÇİŞ ONAYLANDI" : "GEÇİŞ REDDEDİLDİ";
        _result.ForeColor = decision.IsAllowed ? Color.DarkGreen : Color.DarkRed;
        _reason.Text = $"{decision.PersonnelName}  Kart: {decision.CardNumber}  Kapı: {decision.DoorName}  {local:dd.MM.yyyy HH:mm}{Environment.NewLine}{decision.Reason}";
        SetStatus(decision.IsAllowed ? "Geçiş onaylandı" : "Geçiş reddedildi");
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(8, 10, 6, 0)
    };

    private sealed record DoorChoice(int Id, string Display);
}
