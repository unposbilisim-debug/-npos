using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class PersonnelListControl : ScreenControl
{
    private readonly ILogger _logger;
    private readonly DataGridView _grid = Grids.Create();
    private readonly TextBox _search = new() { PlaceholderText = "ad / soyad / sicil" };
    private readonly ComboBox _department = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _active = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private bool _ready;

    public PersonnelListControl(IServiceScopeFactory scopes, ILogger<PersonnelListControl> logger)
        : base(scopes, logger)
    {
        _logger = logger;
        _grid.Columns.Add(Grids.Text(nameof(PersonnelRow.PersonnelNumber), "Sicil", 80));
        _grid.Columns.Add(Grids.Text(nameof(PersonnelRow.FirstName), "Ad", 110));
        _grid.Columns.Add(Grids.Text(nameof(PersonnelRow.LastName), "Soyad", 110));
        _grid.Columns.Add(Grids.Text(nameof(PersonnelRow.DepartmentName), "Departman", 140));
        _grid.Columns.Add(Grids.Text(nameof(PersonnelRow.JobTitleName), "Görev", 130));
        _grid.Columns.Add(Grids.Text(nameof(PersonnelRow.Card), "Kart", 110));
        _grid.Columns.Add(Grids.Text(nameof(PersonnelRow.Status), "Durum", 70));
        _grid.CellDoubleClick += async (_, args) =>
        {
            if (args.RowIndex < 0)
                return;
            await EditAsync(Selected()?.Id);
        };

        _search.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _search.Margin = new Padding(0, 8, 8, 0);
        _search.KeyDown += async (_, args) =>
        {
            if (args.KeyCode != Keys.Enter)
                return;
            args.SuppressKeyPress = true;
            await SearchAsync();
        };

        _department.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _department.Margin = new Padding(0, 8, 8, 0);
        _department.SelectedIndexChanged += async (_, _) =>
        {
            if (_ready)
                await SearchAsync();
        };

        _active.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _active.Margin = new Padding(0, 8, 8, 0);
        _active.Items.AddRange(new object[] { "Tümü", "Aktif", "Pasif" });
        _active.SelectedIndex = 0;
        _active.SelectedIndexChanged += async (_, _) =>
        {
            if (_ready)
                await SearchAsync();
        };

        var searchButton = ActionButton("Ara", 64);
        searchButton.Click += async (_, _) => await SearchAsync();
        var createButton = ActionButton("+ Yeni Personel", 130);
        createButton.Click += async (_, _) => await EditAsync(null);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 8,
            Padding = new Padding(0, 0, 0, 8)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bar.Controls.Add(Caption("Ara"), 0, 0);
        bar.Controls.Add(_search, 1, 0);
        bar.Controls.Add(Caption("Departman"), 2, 0);
        bar.Controls.Add(_department, 3, 0);
        bar.Controls.Add(Caption("Durum"), 4, 0);
        bar.Controls.Add(_active, 5, 0);
        bar.Controls.Add(searchButton, 6, 0);
        bar.Controls.Add(createButton, 7, 0);

        Controls.Add(_grid);
        Controls.Add(bar);
        Load += async (_, _) =>
        {
            await LoadFiltersAsync();
            _ready = true;
            await SearchAsync();
        };
    }

    private async Task LoadFiltersAsync()
    {
        IReadOnlyList<LookupItem>? departments = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IPersonnelService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            departments = await service.GetDepartmentsAsync(Cancellation);
        });
        if (!ok || departments == null || IsDisposed)
            return;

        var items = new List<LookupItem> { new() { Id = 0, Name = "Tümü" } };
        items.AddRange(departments);
        _department.DisplayMember = nameof(LookupItem.Display);
        _department.ValueMember = nameof(LookupItem.Id);
        _department.DataSource = items;
    }

    private async Task SearchAsync()
    {
        var text = _search.Text;
        var departmentId = _department.SelectedValue is int id && id > 0 ? id : (int?)null;
        bool? isActive = _active.SelectedIndex switch
        {
            1 => true,
            2 => false,
            _ => null
        };

        IReadOnlyList<PersonnelListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IPersonnelService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.SearchAsync(new PersonnelSearchCriteria
            {
                Text = text,
                DepartmentId = departmentId,
                IsActive = isActive
            }, Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new PersonnelRow
        {
            Id = row.Id,
            PersonnelNumber = row.PersonnelNumber,
            FirstName = row.FirstName,
            LastName = row.LastName,
            DepartmentName = row.DepartmentName,
            JobTitleName = row.JobTitleName,
            Card = row.ActiveCardNumber,
            Status = Catalog.ActiveText(row.IsActive)
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
    }

    private async Task EditAsync(int? id)
    {
        IReadOnlyList<LookupItem>? departments = null;
        IReadOnlyList<LookupItem>? jobs = null;
        PersonnelDetail? detail = null;
        var found = id == null;
        var loaded = await RunAsync(async services =>
        {
            var service = services.GetService<IPersonnelService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            departments = await service.GetDepartmentsAsync(Cancellation);
            jobs = await service.GetJobTitlesAsync(Cancellation);
            if (id is int personnelId)
            {
                detail = await service.GetAsync(personnelId, Cancellation);
                found = detail != null;
            }
        });
        if (!loaded || departments == null || jobs == null || IsDisposed)
            return;
        if (!found)
        {
            MessageBox.Show(this, "Personel bulunamadı.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await SearchAsync();
            return;
        }

        PersonnelEditModel? model = null;
        var accepted = false;
        using (var form = new PersonnelEditForm(detail, departments, jobs, Scopes, _logger))
        {
            accepted = form.ShowDialog(FindForm()) == DialogResult.OK;
            if (accepted)
                model = form.Model;
        }

        if (!accepted || model == null)
        {
            if (detail != null)
                await SearchAsync();
            return;
        }
        if (detail is { IsActive: true } && !model.IsActive)
        {
            var answer = MessageBox.Show(
                this,
                "Personel pasife alınacak. Aktif kartları iptal edilir. Kart kayıtları silinmez. Devam edilsin mi?",
                "UnAccess",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;
        }

        var saved = await RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPersonnelService>();
            if (detail == null)
                await service.CreateAsync(model, Cancellation);
            else
                await service.UpdateAsync(detail.Id, model, Cancellation);
        });
        if (saved)
            await SearchAsync();
    }

    private PersonnelRow? Selected() => _grid.CurrentRow?.DataBoundItem as PersonnelRow;

    private static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 10, 6, 0)
    };

    private static Button ActionButton(string text, int width) => new()
    {
        Text = text,
        Width = width,
        Height = 28,
        Anchor = AnchorStyles.Left | AnchorStyles.Top,
        Margin = new Padding(0, 6, 0, 0)
    };

    private sealed class PersonnelRow
    {
        public int Id { get; init; }
        public string PersonnelNumber { get; init; } = "";
        public string FirstName { get; init; } = "";
        public string LastName { get; init; } = "";
        public string DepartmentName { get; init; } = "";
        public string JobTitleName { get; init; } = "";
        public string Card { get; init; } = "";
        public string Status { get; init; } = "";
    }
}
