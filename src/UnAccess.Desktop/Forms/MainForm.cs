using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

public sealed class MainForm : Form
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Panel _host = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
    private readonly TreeView _menu = new();
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private ScreenControl? _current;
    private string? _currentKey;

    public MainForm(IServiceScopeFactory scopes, ILoggerFactory loggerFactory)
    {
        _scopes = scopes;
        _loggerFactory = loggerFactory;
        Text = "UnAccess - Kartlı Geçiş ve Yetkilendirme";
        Font = SystemFonts.MessageBoxFont;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1180, 720);
        MinimumSize = new Size(1100, 640);
        AutoScaleMode = AutoScaleMode.Font;

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

        _menu.AfterSelect += (_, args) => OnMenuSelect(args.Node);
        Load += (_, _) =>
        {
            var personnel = FindNode(_menu.Nodes, "personnel");
            if (personnel != null)
                _menu.SelectedNode = personnel;
        };
    }

    private void BuildMenu()
    {
        _menu.Nodes.Add(Node("PERSONELLER", "personnel"));
        _menu.Nodes.Add(Node("KARTLAR", "cards"));

        var hospital = Node("HASTANE", null);
        hospital.Nodes.Add(Node("Binalar", "buildings"));
        hospital.Nodes.Add(Node("Katlar", "floors"));
        hospital.Nodes.Add(Node("Bölgeler", "zones"));
        hospital.Nodes.Add(Node("Kapılar", "doors"));
        _menu.Nodes.Add(hospital);

        var devices = Node("CİHAZLAR", null);
        devices.Nodes.Add(Node("Kontrolörler", "controllers"));
        _menu.Nodes.Add(devices);

        var access = Node("YETKİLER", null);
        access.Nodes.Add(Node("Yetki Grupları", "access-groups"));
        access.Nodes.Add(Node("Zaman Planları", "schedules"));
        access.Nodes.Add(Node("Personel İstisnaları", "overrides"));
        _menu.Nodes.Add(access);

        _menu.Nodes.Add(Node("GEÇİŞ SİMÜLATÖRÜ", "simulator"));
        _menu.Nodes.Add(Node("CANLI GEÇİŞ", "live"));
        _menu.Nodes.Add(Node("RAPORLAR", "disabled"));
        _menu.Nodes.Add(Node("SİSTEM", "disabled"));
        _menu.ExpandAll();
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
        if (node?.Tag is not string key)
        {
            node?.Expand();
            return;
        }

        if (key == "disabled")
        {
            _status.Text = "Bu modül sonraki aşamada eklenecektir.";
            return;
        }

        if (_currentKey == key)
            return;

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
        "access-groups" => new AccessGroupListControl(_scopes, _loggerFactory.CreateLogger<AccessGroupListControl>()),
        "schedules" => new TimeScheduleListControl(_scopes, _loggerFactory.CreateLogger<TimeScheduleListControl>()),
        "overrides" => new PersonnelOverrideListControl(_scopes, _loggerFactory.CreateLogger<PersonnelOverrideListControl>()),
        "simulator" => new PassageSimulatorControl(_scopes, _loggerFactory.CreateLogger<PassageSimulatorControl>()),
        "live" => new LiveAccessControl(_scopes, _loggerFactory.CreateLogger<LiveAccessControl>()),
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
}
