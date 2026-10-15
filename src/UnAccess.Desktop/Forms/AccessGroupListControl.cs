using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class AccessGroupListControl : ScreenControl
{
    private readonly ListBox _groups = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TreeView _tree = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly ComboBox _schedules = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Button _edit = ListButtons.Command("Düzenle");
    private readonly Button _toggle = ListButtons.Command("Pasife Al");
    private List<AccessGroupListItem> _items = [];

    public AccessGroupListControl(IServiceScopeFactory scopes, ILogger<AccessGroupListControl> logger)
        : base(scopes, logger)
    {
        var left = new Panel { Dock = DockStyle.Left, Width = 280, Padding = new Padding(0, 0, 8, 0) };
        var create = ListButtons.Command("+ Yeni");
        create.Click += async (_, _) => await EditGroupAsync(null);
        _edit.Click += async (_, _) => await EditGroupAsync(SelectedGroup());
        _toggle.Click += async (_, _) => await ToggleGroupAsync();
        _groups.SelectedIndexChanged += async (_, _) => await LoadTreeAsync();
        left.Controls.Add(_groups);
        left.Controls.Add(ListButtons.Bar(create, _edit, _toggle));

        var zoneAllow = ListButtons.Command("Bölgeye izin");
        var zoneDeny = ListButtons.Command("Bölgeye yasak");
        var zoneClear = ListButtons.Command("Bölge kuralını kaldır");
        var doorAllow = ListButtons.Command("Kapıya izin");
        var doorDeny = ListButtons.Command("Kapıya yasak");
        var doorClear = ListButtons.Command("Kapı kuralını kaldır");
        zoneAllow.Click += async (_, _) => await SaveRuleAsync(zone: true, allowed: true, clear: false);
        zoneDeny.Click += async (_, _) => await SaveRuleAsync(zone: true, allowed: false, clear: false);
        zoneClear.Click += async (_, _) => await SaveRuleAsync(zone: true, allowed: false, clear: true);
        doorAllow.Click += async (_, _) => await SaveRuleAsync(zone: false, allowed: true, clear: false);
        doorDeny.Click += async (_, _) => await SaveRuleAsync(zone: false, allowed: false, clear: false);
        doorClear.Click += async (_, _) => await SaveRuleAsync(zone: false, allowed: false, clear: true);

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 76, WrapContents = true };
        bar.Controls.Add(new Label { Text = "Zaman planı", AutoSize = true, Margin = new Padding(0, 10, 8, 0) });
        _schedules.Margin = new Padding(0, 6, 12, 0);
        bar.Controls.Add(_schedules);
        foreach (var button in new[] { zoneAllow, zoneDeny, zoneClear, doorAllow, doorDeny, doorClear })
        {
            button.Width = 160;
            bar.Controls.Add(button);
        }

        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(_tree);
        host.Controls.Add(bar);
        Controls.Add(host);
        Controls.Add(left);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<AccessGroupListItem>? groups = null;
        IReadOnlyList<TimeScheduleListItem>? schedules = null;
        var ok = await RunAsync(async services =>
        {
            groups = await services.GetRequiredService<IAccessGroupService>().ListAsync(Cancellation);
            schedules = await services.GetRequiredService<ITimeScheduleService>().ListAsync(Cancellation);
        });
        if (!ok || groups == null || schedules == null || IsDisposed)
            return;

        _items = groups.ToList();
        _groups.DataSource = null;
        _groups.DisplayMember = nameof(AccessGroupListItem.Name);
        _groups.DataSource = _items;
        _schedules.DisplayMember = nameof(TimeScheduleListItem.Name);
        _schedules.ValueMember = nameof(TimeScheduleListItem.Id);
        _schedules.DataSource = schedules.Where(item => item.IsActive).ToList();
        UpdateButtons();
        await LoadTreeAsync();
    }

    private async Task LoadTreeAsync()
    {
        var group = SelectedGroup();
        _tree.Nodes.Clear();
        UpdateButtons();
        if (group == null)
            return;

        IReadOnlyList<AccessTreeNode>? nodes = null;
        var ok = await RunAsync(async services =>
        {
            nodes = await services.GetRequiredService<IAccessGroupService>().GetTreeAsync(group.Id, Cancellation);
        });
        if (!ok || nodes == null || IsDisposed)
            return;

        foreach (var node in nodes)
            _tree.Nodes.Add(CreateNode(node));
        _tree.ExpandAll();
        SetStatus(group.Name);
    }

    private static TreeNode CreateNode(AccessTreeNode model)
    {
        var text = model.Title;
        if (model.IsAllowed is bool allowed)
            text += allowed ? $" — İZİN ({model.ScheduleName})" : $" — YASAK ({model.ScheduleName})";
        var node = new TreeNode(text) { Tag = model };
        foreach (var child in model.Children)
            node.Nodes.Add(CreateNode(child));
        return node;
    }

    private async Task EditGroupAsync(AccessGroupListItem? existing)
    {
        AccessGroupEditModel? model;
        using (var form = new AccessGroupEditForm(existing))
        {
            if (form.ShowDialog(FindForm()) != DialogResult.OK)
                return;
            model = form.Model;
        }

        if (model == null)
            return;
        var saved = await RunAsync(async services =>
        {
            await services.GetRequiredService<IAccessGroupService>().SaveAsync(existing?.Id, model, Cancellation);
        });
        if (saved)
            await ReloadAsync();
    }

    private async Task ToggleGroupAsync()
    {
        var group = SelectedGroup();
        if (group == null || !ListButtons.ConfirmToggle(this, group.IsActive))
            return;

        var saved = await RunAsync(async services =>
        {
            await services.GetRequiredService<IAccessGroupService>().SaveAsync(group.Id, new AccessGroupEditModel
            {
                Code = group.Code,
                Name = group.Name,
                Description = group.Description,
                IsActive = !group.IsActive
            }, Cancellation);
        });
        if (saved)
            await ReloadAsync();
    }

    private async Task SaveRuleAsync(bool zone, bool allowed, bool clear)
    {
        var group = SelectedGroup();
        var node = _tree.SelectedNode?.Tag as AccessTreeNode;
        if (group == null || node == null)
            return;
        if (!group.IsActive)
        {
            MessageBox.Show(this, "Pasif yetki grubuna kural eklenemez.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var targetId = zone ? node.ZoneId : node.DoorId;
        if (targetId is not int id || (zone && node.Kind != "Zone") || (!zone && node.Kind != "Door"))
        {
            MessageBox.Show(this, zone ? "Bir bölge seçiniz." : "Bir kapı seçiniz.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int scheduleId = 0;
        if (!clear)
        {
            if (_schedules.SelectedValue is not int selected || selected <= 0)
            {
                MessageBox.Show(this, "Zaman planı seçiniz.", "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            scheduleId = selected;
        }

        var saved = await RunAsync(async services =>
        {
            var service = services.GetRequiredService<IAccessGroupService>();
            if (zone && clear)
                await service.ClearZoneRuleAsync(group.Id, id, Cancellation);
            else if (zone)
                await service.SetZoneRuleAsync(group.Id, id, scheduleId, allowed, Cancellation);
            else if (clear)
                await service.ClearDoorRuleAsync(group.Id, id, Cancellation);
            else
                await service.SetDoorRuleAsync(group.Id, id, scheduleId, allowed, Cancellation);
        });
        if (saved)
            await LoadTreeAsync();
    }

    private AccessGroupListItem? SelectedGroup() => _groups.SelectedItem as AccessGroupListItem;

    private void UpdateButtons()
    {
        var group = SelectedGroup();
        _edit.Enabled = group != null;
        _toggle.Enabled = group != null;
        _toggle.Text = group is { IsActive: false } ? "Aktifleştir" : "Pasife Al";
    }
}

internal sealed class AccessGroupEditForm : Form
{
    private readonly TextBox _code = new() { MaxLength = UnAccess.Core.Common.FieldLengths.Code };
    private readonly TextBox _name = new() { MaxLength = UnAccess.Core.Common.FieldLengths.Name };
    private readonly TextBox _description = new() { MaxLength = UnAccess.Core.Common.FieldLengths.Description };
    private readonly CheckBox _active = new() { Text = "Aktif", Checked = true };

    public AccessGroupEditForm(AccessGroupListItem? existing)
    {
        FormChrome.Apply(this, existing == null ? "Yeni Yetki Grubu" : "Yetki Grubu", new Size(440, 250));
        if (existing != null)
        {
            _code.Text = existing.Code;
            _name.Text = existing.Name;
            _description.Text = existing.Description ?? "";
            _active.Checked = existing.IsActive;
        }

        var fields = FormChrome.Fields(4);
        FormChrome.AddField(fields, 0, "Kod", _code);
        FormChrome.AddField(fields, 1, "Ad", _name);
        FormChrome.AddField(fields, 2, "Açıklama", _description);
        fields.Controls.Add(_active, 1, 3);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Click += (_, _) => SaveModel();
        Controls.Add(fields);
        Controls.Add(buttons);
    }

    public AccessGroupEditModel? Model { get; private set; }

    private void SaveModel()
    {
        try
        {
            Model = new AccessGroupEditModel
            {
                Code = UnAccess.Core.Common.TextRules.Code(_code.Text, "Grup kodu"),
                Name = UnAccess.Core.Common.TextRules.Name(_name.Text, "Grup adı"),
                Description = _description.Text,
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
