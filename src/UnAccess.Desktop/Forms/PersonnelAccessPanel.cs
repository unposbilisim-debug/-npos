using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class PersonnelAccessPanel : UserControl
{
    private readonly int _personnelId;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _logger;
    private readonly DataGridView _groups = Grids.Create();
    private readonly DataGridView _overrides = Grids.Create();
    private readonly DataGridView _effective = Grids.Create();

    public PersonnelAccessPanel(int personnelId, IServiceScopeFactory scopes, ILogger logger)
    {
        _personnelId = personnelId;
        _scopes = scopes;
        _logger = logger;
        Dock = DockStyle.Fill;

        _groups.Columns.Add(Grids.Text(nameof(GroupRow.Code), "Grup", 90));
        _groups.Columns.Add(Grids.Text(nameof(GroupRow.Name), "Ad", 120));
        _groups.Columns.Add(Grids.Text(nameof(GroupRow.ValidFrom), "Başlangıç", 90));
        _groups.Columns.Add(Grids.Text(nameof(GroupRow.ValidUntil), "Bitiş", 90));
        _groups.Columns.Add(Grids.Text(nameof(GroupRow.Status), "Durum", 70));
        _overrides.Columns.Add(Grids.Text(nameof(OverrideRow.Target), "Hedef", 140));
        _overrides.Columns.Add(Grids.Text(nameof(OverrideRow.Result), "Sonuç", 70));
        _overrides.Columns.Add(Grids.Text(nameof(OverrideRow.Schedule), "Plan", 90));
        _overrides.Columns.Add(Grids.Text(nameof(OverrideRow.Reason), "Gerekçe", 140));
        _effective.Columns.Add(Grids.Text(nameof(EffectiveRow.Door), "Kapı", 140));
        _effective.Columns.Add(Grids.Text(nameof(EffectiveRow.Zone), "Bölge", 120));
        _effective.Columns.Add(Grids.Text(nameof(EffectiveRow.Result), "Sonuç", 70));
        _effective.Columns.Add(Grids.Text(nameof(EffectiveRow.Source), "Kaynak", 120));
        _effective.Columns.Add(Grids.Text(nameof(EffectiveRow.Reason), "Sebep", 220));

        var assign = new Button { Text = "Grup ata", Width = 90, Height = 28 };
        var remove = new Button { Text = "Grubu kaldır", Width = 110, Height = 28 };
        assign.Click += async (_, _) => await AssignAsync();
        remove.Click += async (_, _) => await RemoveAsync();
        var groupBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36 };
        groupBar.Controls.Add(assign);
        groupBar.Controls.Add(remove);

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 180, ColumnCount = 2 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var groupHost = Host("Yetki Grupları", _groups, groupBar);
        var overrideHost = Host("Kişisel İstisnalar", _overrides, null);
        top.Controls.Add(groupHost, 0, 0);
        top.Controls.Add(overrideHost, 1, 0);

        var effectiveHost = Host("Efektif Yetkiler", _effective, null);
        effectiveHost.Dock = DockStyle.Fill;
        Controls.Add(effectiveHost);
        Controls.Add(top);
        Load += async (_, _) => await ReloadAsync();
    }

    private static Panel Host(string title, Control body, Control? bar)
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var label = new Label { Text = title, Dock = DockStyle.Top, Height = 22 };
        host.Controls.Add(body);
        if (bar != null)
            host.Controls.Add(bar);
        host.Controls.Add(label);
        return host;
    }

    private async Task ReloadAsync()
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var service = scope.ServiceProvider.GetService<IPersonnelAuthorizationService>()
                ?? throw new UnAccessException(DataMessages.MissingConnectionString);
            var view = await service.GetAsync(_personnelId, CancellationToken.None);
            if (IsDisposed)
                return;

            _groups.DataSource = view.Groups.Select(item => new GroupRow
            {
                AccessGroupId = item.AccessGroupId,
                Code = item.Code,
                Name = item.Name,
                ValidFrom = Catalog.Date(item.ValidFrom),
                ValidUntil = Catalog.Date(item.ValidUntil),
                Status = Catalog.ActiveText(item.IsActive)
            }).ToList();
            _overrides.DataSource = view.Overrides.Where(item => item.IsActive).Select(item => new OverrideRow
            {
                Target = item.TargetName,
                Result = item.ResultText,
                Schedule = item.ScheduleName,
                Reason = item.Reason ?? ""
            }).ToList();
            _effective.DataSource = view.Effective.Select(item => new EffectiveRow
            {
                Door = item.DoorName,
                Zone = item.ZoneName,
                Result = item.ResultText,
                Source = item.SourceName,
                Reason = item.Reason
            }).ToList();
        }
        catch (Exception exception)
        {
            UiErrors.Show(_logger, this, exception);
        }
    }

    private async Task AssignAsync()
    {
        IReadOnlyList<AccessGroupListItem>? groups = null;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            groups = await scope.ServiceProvider.GetRequiredService<IAccessGroupService>().ListAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            UiErrors.Show(_logger, this, exception);
            return;
        }

        using var form = new AssignGroupForm(groups.Where(item => item.IsActive).ToList());
        if (form.ShowDialog(FindForm()) != DialogResult.OK || form.Model == null)
            return;

        form.Model.PersonnelId = _personnelId;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPersonnelAuthorizationService>().AssignGroupAsync(form.Model, CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            UiErrors.Show(_logger, this, exception);
        }
    }

    private async Task RemoveAsync()
    {
        if (_groups.CurrentRow?.DataBoundItem is not GroupRow row)
            return;
        var answer = MessageBox.Show(this, "Yetki grubu ataması kaldırılacak. Devam edilsin mi?", "UnAccess", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
            return;

        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IPersonnelAuthorizationService>().RemoveGroupAsync(_personnelId, row.AccessGroupId, CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            UiErrors.Show(_logger, this, exception);
        }
    }

    private sealed class GroupRow
    {
        public int AccessGroupId { get; init; }
        public string Code { get; init; } = "";
        public string Name { get; init; } = "";
        public string ValidFrom { get; init; } = "";
        public string ValidUntil { get; init; } = "";
        public string Status { get; init; } = "";
    }

    private sealed class OverrideRow
    {
        public string Target { get; init; } = "";
        public string Result { get; init; } = "";
        public string Schedule { get; init; } = "";
        public string Reason { get; init; } = "";
    }

    private sealed class EffectiveRow
    {
        public string Door { get; init; } = "";
        public string Zone { get; init; } = "";
        public string Result { get; init; } = "";
        public string Source { get; init; } = "";
        public string Reason { get; init; } = "";
    }
}

internal sealed class AssignGroupForm : Form
{
    private readonly ComboBox _group = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _until = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true };

    public AssignGroupForm(IReadOnlyList<AccessGroupListItem> groups)
    {
        FormChrome.Apply(this, "Yetki grubu ata", new Size(420, 220));
        _group.DisplayMember = nameof(AccessGroupListItem.Name);
        _group.ValueMember = nameof(AccessGroupListItem.Id);
        _group.DataSource = groups.ToList();
        var fields = FormChrome.Fields(3);
        FormChrome.AddField(fields, 0, "Grup", _group);
        FormChrome.AddField(fields, 1, "Başlangıç", _from);
        FormChrome.AddField(fields, 2, "Bitiş", _until);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) =>
        {
            if (_group.SelectedValue is not int groupId || groupId <= 0)
            {
                MessageBox.Show(this, "Yetki grubu seçiniz.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Model = new PersonnelGroupEditModel
            {
                AccessGroupId = groupId,
                ValidFrom = DateOnly.FromDateTime(_from.Value),
                ValidUntil = _until.Checked ? DateOnly.FromDateTime(_until.Value) : null,
                IsActive = true
            };
            DialogResult = DialogResult.OK;
        };
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public PersonnelGroupEditModel? Model { get; private set; }
}
