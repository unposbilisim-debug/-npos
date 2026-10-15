using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class TimeScheduleListControl : ScreenControl
{
    private readonly ListBox _plans = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _code = new() { MaxLength = FieldLengths.Code, Width = 180 };
    private readonly TextBox _name = new() { MaxLength = FieldLengths.Name, Width = 220 };
    private readonly TextBox _description = new() { MaxLength = FieldLengths.Description, Width = 280 };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true, AutoSize = true };
    private readonly DataGridView _intervals = Grids.Create();
    private List<TimeScheduleListItem> _items = [];
    private List<IntervalRow> _rows = [];
    private int _currentId;

    public TimeScheduleListControl(IServiceScopeFactory scopes, ILogger<TimeScheduleListControl> logger)
        : base(scopes, logger)
    {
        _intervals.Columns.Add(Grids.Text(nameof(IntervalRow.DayText), "Gün", 120));
        _intervals.Columns.Add(Grids.Text(nameof(IntervalRow.StartText), "Başlangıç", 90));
        _intervals.Columns.Add(Grids.Text(nameof(IntervalRow.EndText), "Bitiş", 90));
        var create = ListButtons.Command("+ Yeni");
        var save = ListButtons.Command("Kaydet");
        var add = ListButtons.Command("Aralık ekle");
        var remove = ListButtons.Command("Aralık sil");
        create.Click += (_, _) => ClearEditor();
        save.Click += async (_, _) => await SaveAsync();
        add.Click += (_, _) => AddInterval();
        remove.Click += (_, _) => RemoveInterval();
        _plans.SelectedIndexChanged += async (_, _) => await LoadSelectedAsync();

        var left = new Panel { Dock = DockStyle.Left, Width = 240, Padding = new Padding(0, 0, 8, 0) };
        left.Controls.Add(_plans);
        left.Controls.Add(ListButtons.Bar(create));

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72, WrapContents = true };
        header.Controls.Add(Caption("Kod"));
        header.Controls.Add(_code);
        header.Controls.Add(Caption("Ad"));
        header.Controls.Add(_name);
        header.Controls.Add(Caption("Açıklama"));
        header.Controls.Add(_description);
        header.Controls.Add(_active);
        header.Controls.Add(save);
        header.Controls.Add(add);
        header.Controls.Add(remove);

        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(_intervals);
        host.Controls.Add(header);
        Controls.Add(host);
        Controls.Add(left);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<TimeScheduleListItem>? plans = null;
        var ok = await RunAsync(async services =>
        {
            plans = await services.GetRequiredService<ITimeScheduleService>().ListAsync(Cancellation);
        });
        if (!ok || plans == null || IsDisposed)
            return;

        _items = plans.ToList();
        _plans.DataSource = null;
        _plans.DisplayMember = nameof(TimeScheduleListItem.Name);
        _plans.DataSource = _items;
        SetStatus($"{_items.Count} plan");
    }

    private async Task LoadSelectedAsync()
    {
        if (_plans.SelectedItem is not TimeScheduleListItem item)
            return;

        TimeScheduleEditModel? model = null;
        var ok = await RunAsync(async services =>
        {
            model = await services.GetRequiredService<ITimeScheduleService>().GetAsync(item.Id, Cancellation);
        });
        if (!ok || model == null || IsDisposed)
            return;

        _currentId = model.Id;
        _code.Text = model.Code;
        _name.Text = model.Name;
        _description.Text = model.Description ?? "";
        _active.Checked = model.IsActive;
        _rows = model.Intervals.Select(interval => new IntervalRow
        {
            Id = interval.Id,
            Day = interval.DayOfWeek,
            Start = interval.StartTime,
            End = interval.EndTime
        }).ToList();
        BindRows();
    }

    private void ClearEditor()
    {
        _plans.ClearSelected();
        _currentId = 0;
        _code.Text = "";
        _name.Text = "";
        _description.Text = "";
        _active.Checked = true;
        _rows = [];
        BindRows();
    }

    private void AddInterval()
    {
        using var form = new IntervalEditForm();
        if (form.ShowDialog(FindForm()) != DialogResult.OK || form.Row == null)
            return;
        _rows.Add(form.Row);
        BindRows();
    }

    private void RemoveInterval()
    {
        if (_intervals.CurrentRow?.DataBoundItem is not IntervalRow row)
            return;
        _rows.Remove(row);
        BindRows();
    }

    private async Task SaveAsync()
    {
        TimeScheduleEditModel? model;
        try
        {
            model = new TimeScheduleEditModel
            {
                Id = _currentId,
                Code = TextRules.Code(_code.Text, "Plan kodu"),
                Name = TextRules.Name(_name.Text, "Plan adı"),
                Description = _description.Text,
                IsActive = _active.Checked,
                Intervals = _rows.Select(row => new TimeIntervalModel
                {
                    Id = row.Id,
                    DayOfWeek = row.Day,
                    StartTime = row.Start,
                    EndTime = row.End
                }).ToList()
            };
        }
        catch (UnAccessException exception)
        {
            MessageBox.Show(this, exception.UserMessage, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var saved = await RunAsync(async services =>
        {
            var result = await services.GetRequiredService<ITimeScheduleService>().SaveAsync(model, Cancellation);
            _currentId = result.Id;
        });
        if (saved)
            await ReloadAsync();
    }

    private void BindRows()
    {
        _intervals.DataSource = null;
        _intervals.DataSource = _rows.Select(row => row).ToList();
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 10, 6, 0)
    };

    internal sealed class IntervalRow
    {
        public int Id { get; init; }
        public DayOfWeek Day { get; init; }
        public TimeOnly Start { get; init; }
        public TimeOnly End { get; init; }
        public string DayText => TurkishCalendar.Day(Day);
        public string StartText => Start.ToString("HH:mm");
        public string EndText => End.ToString("HH:mm");
    }
}

internal sealed class IntervalEditForm : Form
{
    private readonly ComboBox _day = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _start = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 120 };
    private readonly DateTimePicker _end = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 120 };

    public IntervalEditForm()
    {
        FormChrome.Apply(this, "Zaman aralığı", new Size(420, 230));
        _day.DataSource = Enum.GetValues<DayOfWeek>().Cast<DayOfWeek>()
            .OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)
            .Select(day => new DayChoice(day, TurkishCalendar.Day(day)))
            .ToList();
        _day.DisplayMember = nameof(DayChoice.Text);
        var fields = FormChrome.Fields(4);
        FormChrome.AddField(fields, 0, "Gün", _day);
        FormChrome.AddField(fields, 1, "Başlangıç", _start);
        FormChrome.AddField(fields, 2, "Bitiş", _end);
        fields.Controls.Add(new Label
        {
            Text = "Bitiş başlangıçtan küçükse aralık gece yarısını aşar. Eşit saatler tüm günü kapsar.",
            AutoSize = true,
            MaximumSize = new Size(240, 0)
        }, 1, 3);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) =>
        {
            if (_day.SelectedItem is not DayChoice choice)
                return;
            Row = new TimeScheduleListControl.IntervalRow
            {
                Day = choice.Day,
                Start = TimeOnly.FromDateTime(_start.Value),
                End = TimeOnly.FromDateTime(_end.Value)
            };
            DialogResult = DialogResult.OK;
        };
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public TimeScheduleListControl.IntervalRow? Row { get; private set; }

    private sealed record DayChoice(DayOfWeek Day, string Text);
}
