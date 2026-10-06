using UnposVardiyaTakip.Core;

namespace UnposVardiyaTakip.Win;

public sealed class MainForm : Form
{
    private readonly IngestService _ingest = new();
    private readonly FolderWatcher _watcher = new();
    private readonly DataGridView _grid = Theme.Grid();
    private readonly ListBox _logs = new();
    private readonly Label _watchLabel = new();
    private readonly Label _statShifts = Stat("Vardiya", "0");
    private readonly Label _statOpen = Stat("Açık", "0");
    private readonly Label _statLitre = Stat("Litre", "0");
    private readonly Label _statTutar = Stat("Tutar", "0 ₺");
    private List<ShiftRow> _rows = [];

    public MainForm()
    {
        Text = "ünpos Vardiya takip";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 640);
        Theme.Apply(this);

        var header = new Panel { Dock = DockStyle.Top, Height = 78, Padding = new Padding(20, 16, 20, 8) };
        var title = new Label
        {
            Text = "ünpos Vardiya takip",
            Font = new Font("Segoe UI Semibold", 18F),
            ForeColor = Theme.Gold,
            AutoSize = true,
            Location = new Point(20, 12),
        };
        _watchLabel.AutoSize = true;
        _watchLabel.ForeColor = Theme.Muted;
        _watchLabel.Location = new Point(24, 48);
        header.Controls.Add(title);
        header.Controls.Add(_watchLabel);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(16, 8, 16, 8),
            WrapContents = false,
        };
        var tara = Theme.GoldButton("Klasörü tara");
        var ornek = Theme.GhostButton("Örnek vardiya yükle");
        var ac = Theme.GhostButton("Dosya aç");
        var ayar = Theme.GhostButton("Ayarlar");
        tara.Click += (_, _) => Scan();
        ornek.Click += (_, _) => LoadSamples();
        ac.Click += (_, _) => OpenFile();
        ayar.Click += (_, _) => { using var f = new SettingsForm(); f.ShowDialog(this); RefreshAll(); };
        actions.Controls.AddRange(new Control[] { tara, ornek, ac, ayar });

        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 88,
            ColumnCount = 4,
            Padding = new Padding(16, 0, 16, 8),
        };
        for (var i = 0; i < 4; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        stats.Controls.Add(WrapStat(_statShifts), 0, 0);
        stats.Controls.Add(WrapStat(_statOpen), 1, 0);
        stats.Controls.Add(WrapStat(_statLitre), 2, 0);
        stats.Controls.Add(WrapStat(_statTutar), 3, 0);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tarih", HeaderText = "Tarih" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Vardiya", HeaderText = "Vardiya" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Istasyon", HeaderText = "İstasyon" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Durum", HeaderText = "Durum" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Satis", HeaderText = "Satış" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Litre", HeaderText = "Litre" });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tutar", HeaderText = "Tutar" });
        _grid.CellDoubleClick += (_, _) => OpenSelected();

        var gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 8, 16) };
        gridPanel.Controls.Add(_grid);

        _logs.Dock = DockStyle.Fill;
        _logs.BackColor = Theme.Panel;
        _logs.ForeColor = Theme.Muted;
        _logs.BorderStyle = BorderStyle.None;
        _logs.Font = new Font("Consolas", 9F);
        var logPanel = new Panel { Dock = DockStyle.Right, Width = 340, Padding = new Padding(8, 0, 16, 16) };
        var logTitle = new Label { Text = "Aktarım günlüğü", Dock = DockStyle.Top, Height = 28, ForeColor = Theme.Gold };
        logPanel.Controls.Add(_logs);
        logPanel.Controls.Add(logTitle);

        Controls.Add(gridPanel);
        Controls.Add(logPanel);
        Controls.Add(stats);
        Controls.Add(actions);
        Controls.Add(header);

        Load += (_, _) =>
        {
            _watcher.Changed += () => BeginInvoke(RefreshAll);
            _watcher.Start();
            RefreshAll();
        };
        FormClosed += (_, _) => _watcher.Dispose();
    }

    private static Label Stat(string caption, string value)
    {
        return new Label
        {
            Text = $"{caption}\n{value}",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 12F),
            Padding = new Padding(12),
        };
    }

    private static Panel WrapStat(Label label)
    {
        return new Panel { BackColor = Theme.Panel, Margin = new Padding(4), Padding = new Padding(4), Controls = { label } };
    }

    private void Scan()
    {
        _watcher.ScanOnce();
        RefreshAll();
    }

    private void LoadSamples()
    {
        var dir = AppPaths.SamplesDir;
        if (!Directory.Exists(dir))
        {
            MessageBox.Show(this, $"Örnek klasör bulunamadı:\n{dir}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        foreach (var file in Directory.GetFiles(dir).Where(IngestService.IsWatchFile).OrderBy(f => f))
            _ingest.Ingest(file);
        RefreshAll();
    }

    private void OpenFile()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Otomasyon dosyaları|*.xml;*.txt;*.csv;*.zip;*.gz|Tümü|*.*",
            Title = "Vardiya dosyası seç",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var result = _ingest.Ingest(dlg.FileName);
        if (result.Status == "error")
            MessageBox.Show(this, result.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        RefreshAll();
    }

    private void OpenSelected()
    {
        if (_grid.CurrentRow is null) return;
        var index = _grid.CurrentRow.Index;
        if (index < 0 || index >= _rows.Count) return;
        using var detail = new ShiftDetailForm(_rows[index].Id);
        detail.ShowDialog(this);
    }

    private void RefreshAll()
    {
        var cfg = AppConfig.Load();
        var st = _watcher.Status;
        _watchLabel.Text = string.IsNullOrWhiteSpace(st.WatchPath)
            ? $"{cfg.StationName}  ·  izleme klasörü yok  ·  {st.Message}"
            : $"{cfg.StationName}  ·  {st.WatchPath}  ·  {st.Message}";

        var totals = _ingest.Totals();
        _statShifts.Text = $"Vardiya\n{totals.Shifts}";
        _statOpen.Text = $"Açık\n{totals.Open}";
        _statLitre.Text = $"Litre\n{FieldHelper.Liter(totals.Volume)} L";
        _statTutar.Text = $"Tutar\n{FieldHelper.Money(totals.Amount)} ₺";

        _rows = _ingest.ListShifts();
        _grid.Rows.Clear();
        foreach (var row in _rows)
        {
            _grid.Rows.Add(
                row.ShiftDate,
                "V" + row.ShiftNo,
                row.StationName,
                row.Status == "open" ? "Açık" : "Kapalı",
                row.Count,
                FieldHelper.Liter(row.TotalVolume),
                FieldHelper.Money(row.TotalAmount) + " ₺");
        }

        _logs.Items.Clear();
        foreach (var log in _ingest.RecentLogs())
            _logs.Items.Add($"{log.Status,-6}  {Path.GetFileName(log.Path)}  {log.Message}");
    }
}
