using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Common;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class BuildingListControl : ScreenControl
{
    private readonly DataGridView _grid = Grids.Create();
    private readonly Button _edit = ListButtons.Command("Düzenle");
    private readonly Button _toggle = ListButtons.Command("Pasife Al");

    public BuildingListControl(IServiceScopeFactory scopes, ILogger<BuildingListControl> logger)
        : base(scopes, logger)
    {
        _grid.Columns.Add(Grids.Text(nameof(Row.Code), "Kod", 80));
        _grid.Columns.Add(Grids.Text(nameof(Row.Name), "Ad", 220));
        _grid.Columns.Add(Grids.Text(nameof(Row.Status), "Durum", 80));
        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += async (_, args) =>
        {
            if (args.RowIndex >= 0)
                await EditAsync(Selected());
        };
        _edit.Click += async (_, _) => await EditAsync(Selected());
        _toggle.Click += async (_, _) => await ToggleAsync();
        var create = ListButtons.Command("+ Yeni");
        create.Click += async (_, _) => await EditAsync(null);

        Controls.Add(_grid);
        Controls.Add(ListButtons.Bar(create, _edit, _toggle));
        UpdateButtons();
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<BuildingListItem>? rows = null;
        var ok = await RunAsync(async services =>
        {
            var service = services.GetService<IHospitalStructureService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            rows = await service.ListBuildingsAsync(Cancellation);
        });
        if (!ok || rows == null || IsDisposed)
            return;

        _grid.DataSource = rows.Select(row => new Row
        {
            Id = row.Id,
            Code = row.Code,
            Name = row.Name,
            IsActive = row.IsActive,
            Status = Catalog.ActiveText(row.IsActive)
        }).ToList();
        SetStatus($"{rows.Count} kayıt");
        UpdateButtons();
    }

    private async Task EditAsync(Row? existing)
    {
        BuildingEditModel? model;
        using (var form = new BuildingEditForm(existing?.Code, existing?.Name, existing?.IsActive ?? true))
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
                await service.CreateBuildingAsync(model, Cancellation);
            else
                await service.UpdateBuildingAsync(existing.Id, model, Cancellation);
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
            await service.UpdateBuildingAsync(row.Id, new BuildingEditModel
            {
                Code = row.Code,
                Name = row.Name,
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
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsActive { get; init; }
        public string Status { get; init; } = "";
    }
}

internal sealed class BuildingEditForm : Form
{
    private readonly TextBox _code = new() { MaxLength = FieldLengths.Code };
    private readonly TextBox _name = new() { MaxLength = FieldLengths.Name };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true };

    public BuildingEditForm(string? code, string? name, bool isActive)
    {
        FormChrome.Apply(this, code == null ? "Yeni Bina" : "Bina", new Size(420, 220));
        if (code != null)
        {
            _code.Text = code;
            _name.Text = name ?? "";
            _active.Checked = isActive;
        }

        var fields = FormChrome.Fields(3);
        FormChrome.AddField(fields, 0, "Kod", _code);
        FormChrome.AddField(fields, 1, "Ad", _name);
        fields.Controls.Add(_active, 1, 2);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public BuildingEditModel? Model { get; private set; }

    private void SaveModel()
    {
        try
        {
            Model = new BuildingEditModel
            {
                Code = TextRules.Code(_code.Text, "Bina kodu"),
                Name = TextRules.Name(_name.Text, "Bina adı"),
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

internal static class ListButtons
{
    public static Button Command(string text) => new()
    {
        Text = text,
        Width = 110,
        Height = 28,
        Margin = new Padding(0, 4, 8, 8)
    };

    public static FlowLayoutPanel Bar(params Button[] buttons)
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 4)
        };
        foreach (var button in buttons)
            bar.Controls.Add(button);
        return bar;
    }

    public static bool ConfirmToggle(IWin32Window owner, bool isActive)
    {
        var message = isActive
            ? "Kayıt pasife alınacak. Fiziksel silme yapılmaz. Devam edilsin mi?"
            : "Kayıt aktifleştirilecek. Devam edilsin mi?";
        return MessageBox.Show(owner, message, "UnAccess", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
