using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class PersonnelEditForm : Form
{
    private readonly PersonnelDetail? _existing;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _logger;
    private readonly TextBox _number = new() { MaxLength = FieldLengths.PersonnelNumber };
    private readonly TextBox _firstName = new() { MaxLength = FieldLengths.PersonName };
    private readonly TextBox _lastName = new() { MaxLength = FieldLengths.PersonName };
    private readonly ComboBox _department = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _jobTitle = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _phone = new() { MaxLength = FieldLengths.Phone };
    private readonly TextBox _email = new() { MaxLength = FieldLengths.Email };
    private readonly TextBox _photo = new() { MaxLength = FieldLengths.PhotoPath };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true };
    private readonly DataGridView _cards = Grids.Create();

    public PersonnelEditForm(
        PersonnelDetail? existing,
        IReadOnlyList<LookupItem> departments,
        IReadOnlyList<LookupItem> jobTitles,
        IServiceScopeFactory scopes,
        ILogger logger)
    {
        _existing = existing;
        _scopes = scopes;
        _logger = logger;
        FormChrome.Apply(this, existing == null ? "Yeni Personel" : "Personel", new Size(780, 680));

        BindLookup(_department, departments, existing?.DepartmentId);
        BindLookup(_jobTitle, jobTitles, existing?.JobTitleId);
        if (existing != null)
        {
            _number.Text = existing.PersonnelNumber;
            _firstName.Text = existing.FirstName;
            _lastName.Text = existing.LastName;
            _phone.Text = existing.Phone ?? "";
            _email.Text = existing.Email ?? "";
            _photo.Text = existing.PhotoPath ?? "";
            _active.Checked = existing.IsActive;
        }

        var fields = FormChrome.Fields(9);
        FormChrome.AddField(fields, 0, "Sicil", _number);
        FormChrome.AddField(fields, 1, "Ad", _firstName);
        FormChrome.AddField(fields, 2, "Soyad", _lastName);
        FormChrome.AddField(fields, 3, "Departman", _department);
        FormChrome.AddField(fields, 4, "Görev", _jobTitle);
        FormChrome.AddField(fields, 5, "Telefon", _phone);
        FormChrome.AddField(fields, 6, "E-posta", _email);

        var photoHost = new Panel
        {
            Height = 26,
            Margin = new Padding(0, 4, 0, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };
        var browse = new Button { Text = "Gözat...", Width = 84, Dock = DockStyle.Right };
        browse.Click += (_, _) => BrowsePhoto();
        _photo.Dock = DockStyle.Fill;
        photoHost.Controls.Add(_photo);
        photoHost.Controls.Add(browse);
        FormChrome.AddField(fields, 7, "Fotoğraf", photoHost);
        fields.Controls.Add(_active, 1, 8);

        var general = new TabPage("Genel Bilgiler") { Padding = new Padding(4) };
        general.Controls.Add(fields);

        var cardsPage = new TabPage("Kartlar") { Padding = new Padding(4) };
        if (existing == null)
        {
            cardsPage.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Personel kaydedildikten sonra kartlar bu sekmede görünür."
            });
        }
        else
        {
            _cards.Columns.Add(Grids.Text(nameof(CardRow.CardNumber), "Kart No", 120));
            _cards.Columns.Add(Grids.Text(nameof(CardRow.Status), "Durum", 80));
            _cards.Columns.Add(Grids.Text(nameof(CardRow.ValidFrom), "Başlangıç", 90));
            _cards.Columns.Add(Grids.Text(nameof(CardRow.ValidUntil), "Bitiş", 90));
            _cards.Columns.Add(Grids.Text(nameof(CardRow.Description), "Açıklama", 160));
            BindCards(existing.Cards);
            var add = new Button
            {
                Text = "Yeni Kart",
                Width = 100,
                Height = 28,
                Enabled = existing.IsActive,
                Margin = new Padding(0, 4, 0, 8)
            };
            add.Click += async (_, _) => await AddCardAsync();
            var cardBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            cardBar.Controls.Add(add);
            cardsPage.Controls.Add(_cards);
            cardsPage.Controls.Add(cardBar);
        }

        var accessPage = new TabPage("Yetkiler") { Padding = new Padding(4) };
        var canAccess = false;
        using (var scope = scopes.CreateScope())
            canAccess = scope.ServiceProvider.GetRequiredService<IAccessGuard>().Can(Permissions.AccessView);
        if (existing == null)
        {
            accessPage.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Personel kaydedildikten sonra yetkiler bu sekmede görünür."
            });
        }
        else if (!canAccess)
        {
            accessPage.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Yetkileri görüntüleme izniniz yok."
            });
        }
        else
        {
            accessPage.Controls.Add(new PersonnelAccessPanel(existing.Id, scopes, logger));
        }

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(general);
        tabs.TabPages.Add(cardsPage);
        tabs.TabPages.Add(accessPage);

        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(tabs);
        Controls.Add(buttons);
    }

    public PersonnelEditModel? Model { get; private set; }

    private async Task AddCardAsync()
    {
        if (_existing == null)
            return;

        IReadOnlyList<PersonnelOption>? options = null;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var service = scope.ServiceProvider.GetService<ICardService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            options = await service.GetPersonnelOptionsAsync(CancellationToken.None);
            if (!options.Any(option => option.Id == _existing.Id))
            {
                MessageBox.Show(this, "Pasif personele kart verilemez.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new CardEditForm(options, _existing.Id);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Model == null)
                return;

            await service.CreateAsync(dialog.Model, CancellationToken.None);
            var personnel = scope.ServiceProvider.GetRequiredService<IPersonnelService>();
            var detail = await personnel.GetAsync(_existing.Id, CancellationToken.None);
            if (detail != null)
                BindCards(detail.Cards);
        }
        catch (Exception exception)
        {
            UiErrors.Show(_logger, this, exception);
        }
    }

    private void SaveModel()
    {
        try
        {
            Model = new PersonnelEditModel
            {
                PersonnelNumber = TextRules.PersonnelNumber(_number.Text),
                FirstName = TextRules.PersonName(_firstName.Text, "Ad"),
                LastName = TextRules.PersonName(_lastName.Text, "Soyad"),
                DepartmentId = RequiredSelection(_department, "Departman seçiniz."),
                JobTitleId = RequiredSelection(_jobTitle, "Görev seçiniz."),
                Phone = _phone.Text,
                Email = _email.Text,
                PhotoPath = _photo.Text,
                IsActive = _active.Checked
            };
            DialogResult = DialogResult.OK;
        }
        catch (UnAccessException exception)
        {
            MessageBox.Show(this, exception.UserMessage, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void BrowsePhoto()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Fotoğraf seç",
            Filter = "Resimler|*.jpg;*.jpeg;*.png;*.bmp|Tüm dosyalar|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _photo.Text = dialog.FileName;
    }

    private void BindCards(IReadOnlyList<PersonnelCardItem> cards)
    {
        _cards.DataSource = cards.Select(card => new CardRow
        {
            CardNumber = card.CardNumber,
            Status = card.Status.ToTurkish(),
            ValidFrom = Catalog.Date(card.ValidFrom),
            ValidUntil = Catalog.Date(card.ValidUntil),
            Description = card.Description ?? ""
        }).ToList();
    }

    private static void BindLookup(ComboBox combo, IReadOnlyList<LookupItem> items, int? currentId)
    {
        var choices = items.Where(item => item.IsActive || item.Id == currentId).ToList();
        combo.DisplayMember = nameof(LookupItem.Display);
        combo.ValueMember = nameof(LookupItem.Id);
        combo.DataSource = choices;
        if (currentId is int id)
            combo.SelectedValue = id;
    }

    private static int RequiredSelection(ComboBox combo, string message)
    {
        if (combo.SelectedValue is not int id || id <= 0)
            throw new UnAccessException(message);
        return id;
    }

    private sealed class CardRow
    {
        public string CardNumber { get; init; } = "";
        public string Status { get; init; } = "";
        public string ValidFrom { get; init; } = "";
        public string ValidUntil { get; init; } = "";
        public string Description { get; init; } = "";
    }
}
