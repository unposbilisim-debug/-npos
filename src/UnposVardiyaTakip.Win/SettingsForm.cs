using UnposVardiyaTakip.Core;

namespace UnposVardiyaTakip.Win;

public sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text = "Ayarlar";
        Width = 680;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Theme.Apply(this);

        var cfg = AppConfig.Load();
        var station = Box(cfg.StationName, 24, 48, 610);
        var path = Box(cfg.WatchPath, 24, 118, 470);
        var browse = Theme.GhostButton("Klasör seç");
        browse.Location = new Point(510, 114);
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Türpak / Asis export klasörü" };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                path.Text = dlg.SelectedPath;
        };

        var brand = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Left = 24, Top = 200, Width = 180,
            BackColor = Color.FromArgb(10, 20, 32),
            ForeColor = Theme.Text,
        };
        brand.Items.AddRange(new object[] { "auto", "turpak", "asis" });
        brand.SelectedItem = cfg.Brand is "turpak" or "asis" or "auto" ? cfg.Brand : "auto";

        var poll = Number(cfg.PollSeconds, 230, 200);
        var vol = Number(cfg.VolumeDivisor, 24, 280);
        var amt = Number(cfg.AmountDivisor, 230, 280);
        var price = Number(cfg.PriceDivisor, 430, 280);

        var save = Theme.GoldButton("Kaydet");
        save.Location = new Point(24, 410);
        save.Click += (_, _) =>
        {
            cfg.StationName = string.IsNullOrWhiteSpace(station.Text) ? "İstasyon" : station.Text.Trim();
            cfg.WatchPath = path.Text.Trim();
            cfg.Brand = brand.SelectedItem?.ToString() ?? "auto";
            cfg.PollSeconds = (int)poll.Value;
            cfg.VolumeDivisor = (int)vol.Value;
            cfg.AmountDivisor = (int)amt.Value;
            cfg.PriceDivisor = (int)price.Value;
            cfg.Save();
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[]
        {
            Caption("İstasyon adı", 24, 24),
            station,
            Caption("Otomasyon klasörü (\\\\PC\\shift)", 24, 94),
            path,
            browse,
            Caption("Otomasyon", 24, 176),
            brand,
            Caption("Tarama (sn)", 230, 176),
            poll,
            Caption("Litre böleni", 24, 256),
            vol,
            Caption("Tutar böleni", 230, 256),
            amt,
            Caption("Fiyat böleni", 430, 256),
            price,
            Caption("Otomasyon PC'de klasörü salt okunur paylaşın. Bu program SQL'e yazmaz.", 24, 340),
            save
        });
    }

    private static Label Caption(string text, int x, int y) =>
        new() { Text = text, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(x, y) };

    private static TextBox Box(string value, int x, int y, int w) =>
        new()
        {
            Text = value,
            Left = x,
            Top = y,
            Width = w,
            BackColor = Color.FromArgb(10, 20, 32),
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
        };

    private static NumericUpDown Number(int value, int x, int y) =>
        new()
        {
            Left = x,
            Top = y,
            Width = 160,
            Minimum = 1,
            Maximum = 100000,
            Value = Math.Clamp(value, 1, 100000),
            BackColor = Color.FromArgb(10, 20, 32),
            ForeColor = Theme.Text,
        };
}
