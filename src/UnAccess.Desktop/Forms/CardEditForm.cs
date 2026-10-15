using UnAccess.Core.Common;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class CardEditForm : Form
{
    private readonly ComboBox _personnel = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _number = new() { MaxLength = FieldLengths.CardNumber };
    private readonly DateTimePicker _validFrom = new() { Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _validUntil = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly TextBox _description = new() { MaxLength = FieldLengths.Description };

    public CardEditForm(IReadOnlyList<PersonnelOption> personnel, int? lockedPersonnelId)
    {
        FormChrome.Apply(this, "Yeni Kart", new Size(480, 280));
        _personnel.DisplayMember = nameof(PersonnelOption.Display);
        _personnel.ValueMember = nameof(PersonnelOption.Id);
        _personnel.DataSource = personnel.ToList();
        if (lockedPersonnelId is int id)
        {
            _personnel.SelectedValue = id;
            _personnel.Enabled = false;
        }

        var fields = FormChrome.Fields(5);
        FormChrome.AddField(fields, 0, "Personel", _personnel);
        FormChrome.AddField(fields, 1, "Kart No", _number);
        FormChrome.AddField(fields, 2, "Başlangıç", _validFrom);
        FormChrome.AddField(fields, 3, "Bitiş", _validUntil);
        FormChrome.AddField(fields, 4, "Açıklama", _description);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public CardEditModel? Model { get; private set; }

    private void SaveModel()
    {
        try
        {
            if (_personnel.SelectedValue is not int personnelId || personnelId <= 0)
                throw new UnAccessException("Personel seçiniz.");

            Model = new CardEditModel
            {
                PersonnelId = personnelId,
                CardNumber = TextRules.CardNumber(_number.Text),
                ValidFrom = DateOnly.FromDateTime(_validFrom.Value.Date),
                ValidUntil = _validUntil.Checked ? DateOnly.FromDateTime(_validUntil.Value.Date) : null,
                Description = _description.Text
            };
            DialogResult = DialogResult.OK;
        }
        catch (UnAccessException exception)
        {
            MessageBox.Show(this, exception.UserMessage, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
