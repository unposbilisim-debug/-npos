using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class BackupControl : ScreenControl
{
    private readonly Label _status = Line();
    private readonly Label _last = Line();
    private readonly Label _file = Line();
    private readonly Label _folder = Line();
    private readonly Label _automatic = Line();
    private readonly Label _time = Line();
    private readonly Label _retention = Line();
    private readonly Label _maintenance = new()
    {
        Text = "Bakım Modu / Geri Yükleme İşlemi",
        AutoSize = true,
        Visible = false,
        ForeColor = Color.DarkRed,
        Margin = new Padding(8, 8, 8, 0)
    };
    private readonly Button _backupNow;
    private readonly Button _restore;

    public BackupControl(IServiceScopeFactory scopes, ILogger<BackupControl> logger)
        : base(scopes, logger)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Add(table, 0, "Yedekleme Durumu", _status);
        Add(table, 1, "Son Başarılı Yedek", _last);
        Add(table, 2, "Son Yedek Dosyası", _file);
        Add(table, 3, "Yedek Klasörü", _folder);
        Add(table, 4, "Otomatik Yedekleme", _automatic);
        Add(table, 5, "Yedekleme Saati", _time);
        Add(table, 6, "Saklama Süresi", _retention);
        _backupNow = new Button { Text = "ŞİMDİ YEDEK AL", Width = 150, Height = 28, Margin = new Padding(0, 8, 8, 0) };
        _restore = new Button { Text = "YEDEKTEN GERİ YÜKLE", Width = 180, Height = 28, Margin = new Padding(0, 8, 8, 0) };
        var refresh = new Button { Text = "Yenile", Width = 96, Height = 28, Margin = new Padding(0, 8, 0, 0) };
        _backupNow.Click += async (_, _) => await BackupNowAsync();
        _restore.Click += async (_, _) => await RestoreAsync();
        refresh.Click += async (_, _) => await ReloadAsync();
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        bar.Controls.Add(_backupNow);
        bar.Controls.Add(_restore);
        bar.Controls.Add(refresh);
        Controls.Add(_maintenance);
        Controls.Add(table);
        Controls.Add(bar);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var canManage = false;
        var ok = await RunAsync(async services =>
        {
            canManage = services.GetRequiredService<IAccessGuard>().Can(Permissions.BackupManage);
            var status = await services.GetRequiredService<IBackupService>().GetStatusAsync(Cancellation);
            _status.Text = status.StatusText;
            _last.Text = status.LastSuccessText;
            _file.Text = status.LastFileName;
            _folder.Text = string.IsNullOrWhiteSpace(status.Directory) ? "Tanımlı değil" : status.Directory + " — " + status.FolderText;
            _automatic.Text = status.Enabled ? "Açık" : "Kapalı";
            _time.Text = status.TimeText;
            _retention.Text = status.RetentionDays + " gün";
        });
        if (!ok || IsDisposed)
            return;
        _backupNow.Enabled = canManage;
        _restore.Enabled = canManage;
        SetStatus("Yedekleme");
    }

    private async Task BackupNowAsync()
    {
        var ok = await RunAsync(async services =>
        {
            var result = await services.GetRequiredService<IBackupService>().CreateManualAsync(Cancellation);
            if (!result.Succeeded)
                throw new UnAccess.Core.Exceptions.UnAccessException(string.IsNullOrWhiteSpace(result.Message) ? "Yedekleme tamamlanamadı." : result.Message);
            MessageBox.Show(this, result.Message, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        if (ok)
            await ReloadAsync();
    }

    private async Task RestoreAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "SQL yedeği (*.bak)|*.bak",
            Title = "Yedek dosyası"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        using var confirm = new RestoreConfirmForm();
        if (confirm.ShowDialog(this) != DialogResult.OK)
            return;

        _maintenance.Visible = true;
        var ok = await RunAsync(async services =>
        {
            var result = await services.GetRequiredService<IRestoreService>().RestoreAsync(new RestoreRequest
            {
                BackupFile = dialog.FileName,
                ConfirmationText = confirm.Confirmation
            }, Cancellation);
            MessageBox.Show(this, result.Message, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        _maintenance.Visible = false;
        if (ok)
            await ReloadAsync();
    }

    private static void Add(TableLayoutPanel table, int row, string caption, Label value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, row);
        table.Controls.Add(value, 1, row);
    }

    private static Label Line() => new() { AutoSize = true, Margin = new Padding(0, 6, 0, 0), Text = "—" };
}

internal sealed class RestoreConfirmForm : Form
{
    private readonly TextBox _confirmation = new() { MaxLength = 32 };
    public string Confirmation => _confirmation.Text;

    public RestoreConfirmForm()
    {
        FormChrome.Apply(this, "Yedekten Geri Yükle", new Size(560, 260));
        var warning = new Label
        {
            Text = BackupMessages.RestoreWarning,
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(12)
        };
        var hint = new Label
        {
            Text = "Devam etmek için " + BackupMessages.ConfirmationText + " yazın.",
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(12, 0, 12, 0)
        };
        _confirmation.Dock = DockStyle.Top;
        _confirmation.Margin = new Padding(12);
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
        host.Controls.Add(_confirmation);
        var buttons = FormChrome.Buttons(this, out var save, out _);
        save.Text = "Geri Yükle";
        save.Click += (_, _) =>
        {
            if (!string.Equals(_confirmation.Text, BackupMessages.ConfirmationText, StringComparison.Ordinal))
            {
                MessageBox.Show(this, BackupMessages.ConfirmationRejected, "UnAccess", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(host);
        Controls.Add(hint);
        Controls.Add(warning);
        Controls.Add(buttons);
    }
}
