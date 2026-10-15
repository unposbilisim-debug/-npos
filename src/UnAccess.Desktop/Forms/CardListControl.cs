using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Enums;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class CardListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();
    private readonly TextBox _search = new() { PlaceholderText = "Kart no / personel / sicil" };
    private readonly Button _active = ActionButton("Aktif");
    private readonly Button _lost = ActionButton("Kayıp");
    private readonly Button _cancelled = ActionButton("İptal");

    public CardListControl(IServiceScopeFactory scopes, ILogger<CardListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(CardRow.CardNumber), "Kart No", 110));
        _grid.Columns.Add(Grids.Text(nameof(CardRow.PersonnelName), "Personel", 150));
        _grid.Columns.Add(Grids.Text(nameof(CardRow.PersonnelNumber), "Sicil", 80));
        _grid.Columns.Add(Grids.Text(nameof(CardRow.DepartmentName), "Departman", 130));
        _grid.Columns.Add(Grids.Text(nameof(CardRow.Status), "Durum", 80));
        _grid.Columns.Add(Grids.Text(nameof(CardRow.ValidFrom), "Başlangıç", 90));
        _grid.Columns.Add(Grids.Text(nameof(CardRow.ValidUntil), "Bitiş", 90));
        _grid.SelectionChanged += (_, _) => UpdateButtons();

        _search.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _search.Margin = new Padding(0, 8, 8, 0);
        _search.KeyDown += async (_, args) =>
        {
            if (args.KeyCode != Keys.Enter)
                return;
            args.SuppressKeyPress = true;
            await SearchAsync();
        };

        var searchButton = ActionButton("Ara");
        searchButton.Click += async (_, _) => await SearchAsync();
        var createButton = ActionButton("+ Yeni Kart");
        createButton.Width = 110;
        createButton.Click += async (_, _) => await CreateAsync();
        _active.Click += async (_, _) => await ChangeStatusAsync(CardStatus.Active);
        _lost.Click += async (_, _) => await ChangeStatusAsync(CardStatus.Lost);
        _cancelled.Click += async (_, _) => await ChangeStatusAsync(CardStatus.Cancelled);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 7,
            Padding = new Padding(0, 0, 0, 8)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bar.Controls.Add(new Label
        {
            Text = "Ara",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 10, 6, 0)
        }, 0, 0);
        bar.Controls.Add(_search, 1, 0);
        bar.Controls.Add(searchButton, 2, 0);
        bar.Controls.Add(createButton, 3, 0);
        bar.Controls.Add(_active, 4, 0);
        bar.Controls.Add(_lost, 5, 0);
        bar.Controls.Add(_cancelled, 6, 0);

        Controls.Add(_grid);
        Controls.Add(bar);
        UpdateButtons();
        Load += async (_, _) => await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var text = _search.Text;
        IReadOnlyList<CardListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<ICardService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.SearchAsync(new CardSearchCriteria { Text = text }, Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new CardRow
        {
            Id = row.Id,
            CardNumber = row.CardNumber,
            PersonnelName = row.PersonnelName,
            PersonnelNumber = row.PersonnelNumber,
            DepartmentName = row.DepartmentName,
            Status = row.Status.ToTurkish(),
            ValidFrom = Catalog.Date(row.ValidFrom),
            ValidUntil = Catalog.Date(row.ValidUntil)
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
        UpdateButtons();
    }

    private async Task CreateAsync()
    {
        IReadOnlyList<PersonnelOption>? options = null;
        var loaded = await RunAsync(async services =>
        {
            var service = services.GetService<ICardService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            options = await service.GetPersonnelOptionsAsync(Cancellation);
        });
        if (!loaded || options == null || IsDisposed)
            return;
        if (options.Count == 0)
        {
            MessageBox.Show(this, "Kart verilebilecek aktif personel yok.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CardEditModel? model;
        using (var form = new CardEditForm(options, null))
        {
            if (form.ShowDialog(FindForm()) != DialogResult.OK)
                return;
            model = form.Model;
        }

        if (model == null)
            return;

        var saved = await RunAsync(async services =>
        {
            var service = services.GetRequiredService<ICardService>();
            await service.CreateAsync(model, Cancellation);
        });
        if (saved)
            await SearchAsync();
    }

    private async Task ChangeStatusAsync(CardStatus status)
    {
        var row = Selected();
        if (row == null)
            return;

        if (status != CardStatus.Active)
        {
            var verb = status == CardStatus.Lost ? "kayıp" : "iptal";
            var answer = MessageBox.Show(
                this,
                $"Kart {verb} olarak işaretlenecek. Personel kaydı silinmez ve eski kart korunur. Devam edilsin mi?",
                "UnAccess",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;
        }

        var saved = await RunAsync(async services =>
        {
            var service = services.GetService<ICardService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            await service.SetStatusAsync(row.Id, status, Cancellation);
        });
        if (saved)
            await SearchAsync();
    }

    private void UpdateButtons()
    {
        var enabled = Selected() != null;
        _active.Enabled = enabled;
        _lost.Enabled = enabled;
        _cancelled.Enabled = enabled;
    }

    private CardRow? Selected() => _grid.CurrentRow?.DataBoundItem as CardRow;

    private static Button ActionButton(string text) => new()
    {
        Text = text,
        Width = 72,
        Height = 28,
        Anchor = AnchorStyles.Left | AnchorStyles.Top,
        Margin = new Padding(0, 6, 0, 0)
    };

    private sealed class CardRow
    {
        public int Id { get; init; }
        public string CardNumber { get; init; } = "";
        public string PersonnelName { get; init; } = "";
        public string PersonnelNumber { get; init; } = "";
        public string DepartmentName { get; init; } = "";
        public string Status { get; init; } = "";
        public string ValidFrom { get; init; } = "";
        public string ValidUntil { get; init; } = "";
    }
}
