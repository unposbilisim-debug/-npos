using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Security;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

public sealed class MainForm : Form
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAccessGuard _access;
    private readonly IRuntimeSettings _settings;
    private readonly Panel _host = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
    private readonly TreeView _menu = new();
    private readonly Label _hospital = new() { AutoSize = true, Text = "Hastane" };
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private ScreenControl? _current;
    private string? _currentKey;

    public MainForm(IServiceScopeFactory scopes, ILoggerFactory loggerFactory, IAccessGuard access, IRuntimeSettings settings)
    {
        _scopes = scopes;
        _loggerFactory = loggerFactory;
        _access = access;
        _settings = settings;
        Text = "UnAccess - Kartlı Geçiş ve Yetkilendirme";
        Font = SystemFonts.MessageBoxFont;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 720);
        MinimumSize = new Size(1100, 640);
        AutoScaleMode = AutoScaleMode.Font;

        var header = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(12, 6, 8, 0) };
        _hospital.Font = new Font(Font, FontStyle.Bold);
        var subtitle = new Label
        {
            Text = "UnAccess Geçiş Kontrol Sistemi",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(0, 20)
        };
        var titles = new Panel { Dock = DockStyle.Fill };
        titles.Controls.Add(subtitle);
        titles.Controls.Add(_hospital);
        header.Controls.Add(titles);

        var menuHost = new Panel
        {
            Dock = DockStyle.Left,
            Width = 230,
            Padding = new Padding(0, 8, 8, 8),
            BackColor = SystemColors.Control
        };
        _menu.Dock = DockStyle.Fill;
        _menu.BorderStyle = BorderStyle.FixedSingle;
        _menu.HideSelection = false;
        _menu.FullRowSelect = true;
        _menu.ShowLines = false;
        _menu.ItemHeight = 22;
        _menu.Indent = 18;
        BuildMenu();
        menuHost.Controls.Add(_menu);

        var status = new StatusStrip();
        _status.Text = "Hazır";
        status.Items.Add(_status);

        Controls.Add(_host);
        Controls.Add(menuHost);
        Controls.Add(status);
        Controls.Add(header);

        _menu.AfterSelect += (_, args) => OnMenuSelect(args.Node);
        Load += async (_, _) =>
        {
            try
            {
                _hospital.Text = await _settings.HospitalNameAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                _loggerFactory.CreateLogger<MainForm>().LogWarning(exception, "Hastane adı okunamadı.");
            }

            var first = FindFirstLeaf(_menu.Nodes);
            if (first != null)
                _menu.SelectedNode = first;
        };
    }

    private void BuildMenu()
    {
        AddLeaf("PERSONELLER", "personnel", Permissions.PersonnelView);
        AddLeaf("KARTLAR", "cards", Permissions.CardsView);
        AddGroup("YETKİLER",
        [
            ("Yetki Grupları", "access-groups", Permissions.AccessView),
            ("Zaman Planları", "schedules", Permissions.AccessView),
            ("Personel İstisnaları", "overrides", Permissions.AccessView)
        ]);
        AddGroup("HASTANE",
        [
            ("Binalar", "buildings", Permissions.HospitalView),
            ("Katlar", "floors", Permissions.HospitalView),
            ("Bölgeler", "zones", Permissions.HospitalView),
            ("Kapılar", "doors", Permissions.HospitalView)
        ]);
        AddGroup("CİHAZLAR",
        [
            ("Kontrolörler", "controllers", Permissions.DevicesView),
            ("Senkronizasyon", "sync", Permissions.DevicesView)
        ]);
        AddGroup("CANLI GEÇİŞ",
        [
            ("Canlı İzleme", "live", Permissions.LiveView),
            ("Geçiş Simülatörü", "simulator", Permissions.PassageSimulate)
        ]);
        AddGroup("RAPORLAR",
        [
            ("Geçiş Hareketleri", "report-passages", Permissions.ReportsView),
            ("Reddedilen Geçişler", "report-denied", Permissions.ReportsView),
            ("Personel Yetki Raporu", "report-personnel", Permissions.ReportsView),
            ("Kapı Yetki Raporu", "report-doors", Permissions.ReportsView)
        ]);
        AddGroup("SİSTEM",
        [
            ("Kullanıcılar", "users", Permissions.UsersManage),
            ("İşlem Geçmişi", "audit", Permissions.AuditView),
            ("Sistem Ayarları", "settings", Permissions.SettingsManage),
            ("Sistem Durumu", "health", Permissions.SystemView),
            ("Yedekleme", "backup", Permissions.SystemView),
            ("Hakkında", "about", Permissions.SystemView)
        ]);
        _menu.ExpandAll();
    }

    private void AddLeaf(string text, string key, string permission)
    {
        if (_access.Can(permission))
            _menu.Nodes.Add(Node(text, key));
    }

    private void AddGroup(string text, (string Caption, string Key, string Permission)[] children)
    {
        var parent = Node(text, null);
        foreach (var child in children)
        {
            if (_access.Can(child.Permission))
                parent.Nodes.Add(Node(child.Caption, child.Key));
        }

        if (parent.Nodes.Count == 1 && parent.Nodes[0].Tag is string only)
        {
            parent.Text = text;
            parent.Tag = only;
            parent.Nodes.Clear();
        }

        if (parent.Nodes.Count > 0 || parent.Tag is string)
            _menu.Nodes.Add(parent);
    }

    private static TreeNode Node(string text, string? key)
    {
        var node = new TreeNode(text);
        if (key != null)
            node.Tag = key;
        return node;
    }

    private void OnMenuSelect(TreeNode? node)
    {
        if (node?.Tag is not string key || _currentKey == key)
        {
            node?.Expand();
            return;
        }

        _host.Controls.Clear();
        if (_current != null)
        {
            _current.StatusTextChanged -= OnStatus;
            _current.Dispose();
        }

        _current = CreateScreen(key);
        _current.Dock = DockStyle.Fill;
        _current.StatusTextChanged += OnStatus;
        _host.Controls.Add(_current);
        _currentKey = key;
        _status.Text = "Hazır";
    }

    private ScreenControl CreateScreen(string key) => key switch
    {
        "personnel" => new PersonnelListControl(_scopes, _loggerFactory.CreateLogger<PersonnelListControl>()),
        "cards" => new CardListControl(_scopes, _loggerFactory.CreateLogger<CardListControl>()),
        "buildings" => new BuildingListControl(_scopes, _loggerFactory.CreateLogger<BuildingListControl>()),
        "floors" => new FloorListControl(_scopes, _loggerFactory.CreateLogger<FloorListControl>()),
        "zones" => new ZoneListControl(_scopes, _loggerFactory.CreateLogger<ZoneListControl>()),
        "doors" => new DoorListControl(_scopes, _loggerFactory.CreateLogger<DoorListControl>()),
        "controllers" => new ControllerListControl(_scopes, _loggerFactory.CreateLogger<ControllerListControl>()),
        "sync" => new DeviceSyncControl(_scopes, _loggerFactory.CreateLogger<DeviceSyncControl>()),
        "access-groups" => new AccessGroupListControl(_scopes, _loggerFactory.CreateLogger<AccessGroupListControl>()),
        "schedules" => new TimeScheduleListControl(_scopes, _loggerFactory.CreateLogger<TimeScheduleListControl>()),
        "overrides" => new PersonnelOverrideListControl(_scopes, _loggerFactory.CreateLogger<PersonnelOverrideListControl>()),
        "simulator" => new PassageSimulatorControl(_scopes, _loggerFactory.CreateLogger<PassageSimulatorControl>()),
        "live" => new LiveAccessControl(_scopes, _loggerFactory.CreateLogger<LiveAccessControl>()),
        "report-passages" => new ReportControl(ReportKind.Passages, _scopes, _loggerFactory.CreateLogger<ReportControl>()),
        "report-denied" => new ReportControl(ReportKind.Denied, _scopes, _loggerFactory.CreateLogger<ReportControl>()),
        "report-personnel" => new ReportControl(ReportKind.PersonnelAccess, _scopes, _loggerFactory.CreateLogger<ReportControl>()),
        "report-doors" => new ReportControl(ReportKind.DoorAccess, _scopes, _loggerFactory.CreateLogger<ReportControl>()),
        "users" => new UserListControl(_scopes, _loggerFactory.CreateLogger<UserListControl>()),
        "audit" => new AuditLogControl(_scopes, _loggerFactory.CreateLogger<AuditLogControl>()),
        "settings" => new SystemSettingsControl(_scopes, _loggerFactory.CreateLogger<SystemSettingsControl>()),
        "health" => new SystemStatusControl(_scopes, _loggerFactory.CreateLogger<SystemStatusControl>()),
        "backup" => new BackupControl(_scopes, _loggerFactory.CreateLogger<BackupControl>()),
        "about" => new AboutControl(_scopes, _loggerFactory.CreateLogger<AboutControl>()),
        _ => throw new InvalidOperationException("Bilinmeyen ekran.")
    };

    private void OnStatus(object? sender, string text) => _status.Text = text;

    private static TreeNode? FindNode(TreeNodeCollection nodes, string key)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag as string == key)
                return node;
            var child = FindNode(node.Nodes, key);
            if (child != null)
                return child;
        }

        return null;
    }

    private static TreeNode? FindFirstLeaf(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is string)
                return node;
            var child = FindFirstLeaf(node.Nodes);
            if (child != null)
                return child;
        }

        return null;
    }
}
