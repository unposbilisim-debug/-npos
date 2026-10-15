using Microsoft.Extensions.DependencyInjection;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Exceptions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;

namespace UnAccess.Desktop.Forms;

internal sealed class SetupWizardForm : Form
{
    private readonly IConnectionProbe _probe;
    private readonly Func<string, ServiceProvider> _factory;
    private readonly IConnectionConfigurationStore _store;
    private readonly Panel _body = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 32, Padding = new Padding(12, 8, 12, 0) };
    private readonly Label _message = new() { Dock = DockStyle.Bottom, Height = 48, ForeColor = Color.DarkRed, Padding = new Padding(12, 0, 12, 0) };
    private readonly Button _back = new() { Text = "Geri", Width = 96, Height = 28 };
    private readonly Button _next = new() { Text = "İleri", Width = 96, Height = 28 };
    private int _step;
    private string? _connection;
    private ServiceProvider? _data;
    private bool _complete;

    private readonly TextBox _server = new();
    private readonly TextBox _instance = new();
    private readonly TextBox _database = new();
    private readonly ComboBox _auth = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _user = new();
    private readonly TextBox _sqlPassword = new() { UseSystemPasswordChar = true };
    private readonly TextBox _hospital = new();
    private readonly TextBox _zone = new() { Text = SecurityDefaults.TimeZone };
    private readonly CheckBox _backupEnabled = new() { Text = "Otomatik yedekleme açık" };
    private readonly TextBox _backupDirectory = new();
    private readonly TextBox _backupTime = new() { Text = SecurityDefaults.BackupTime };
    private readonly NumericUpDown _retention = new() { Minimum = 1, Maximum = 3650, Value = SecurityDefaults.BackupRetentionDays };
    private readonly TextBox _adminUser = new();
    private readonly TextBox _adminName = new();
    private readonly TextBox _adminPassword = new() { UseSystemPasswordChar = true };
    private readonly TextBox _adminRepeat = new() { UseSystemPasswordChar = true };

    public SetupWizardForm(IConnectionProbe probe, Func<string, ServiceProvider> factory, IConnectionConfigurationStore store)
    {
        _probe = probe;
        _factory = factory;
        _store = store;
        Ui.FormChrome.Apply(this, "UnAccess Kurulum", new Size(640, 460));
        _auth.Items.AddRange(new object[] { "SQL Authentication", "Windows Authentication" });
        _auth.SelectedIndex = 0;
        _back.Click += (_, _) => Step(-1);
        _next.Click += async (_, _) => await MoveAsync(1);
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(12, 8, 12, 8)
        };
        buttons.Controls.Add(_next);
        buttons.Controls.Add(_back);
        Controls.Add(_body);
        Controls.Add(_message);
        Controls.Add(_title);
        Controls.Add(buttons);
        ShowStep();
    }

    public string? ConnectionString => _connection;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _data?.Dispose();
        base.Dispose(disposing);
    }

    private void Step(int delta)
    {
        _step = Math.Max(0, _step + delta);
        _message.Text = "";
        ShowStep();
    }

    private async Task MoveAsync(int delta)
    {
        _message.Text = "";
        try
        {
            UseWaitCursor = true;
            if (delta > 0 && !await AcceptStepAsync())
                return;
            if (_step == 7 && delta > 0)
            {
                DialogResult = _complete ? DialogResult.OK : DialogResult.Cancel;
                Close();
                return;
            }

            _step = Math.Min(7, _step + delta);
            ShowStep();
        }
        catch (Exception exception)
        {
            _message.Text = exception is UnAccessException known
                ? known.UserMessage
                : BackupMessages.Unexpected;
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task<bool> AcceptStepAsync()
    {
        switch (_step)
        {
            case 1:
                var probe = await _probe.TestAsync(Endpoint(), CancellationToken.None);
                if (!probe.Succeeded)
                {
                    _message.Text = probe.Message;
                    return false;
                }

                _connection = _probe.BuildConnectionString(Endpoint());
                ReplaceData();
                return true;
            case 2:
                var health = await RequireData().GetRequiredService<IDatabaseHealthService>().GetAsync(CancellationToken.None);
                if (!health.Connected || health.PendingMigrations || !health.TablesReadable)
                {
                    _message.Text = "Veritabanı şeması hazır değil. Şemayı uygulayın.";
                    return false;
                }

                return true;
            case 3:
                if (string.IsNullOrWhiteSpace(_hospital.Text) || string.IsNullOrWhiteSpace(_zone.Text))
                {
                    _message.Text = "Hastane adı ve saat dilimi zorunludur.";
                    return false;
                }

                return true;
            case 4:
                await RequireData().GetRequiredService<ISetupService>().SaveInstallationSettingsAsync(new InstallationSettings
                {
                    HospitalName = _hospital.Text,
                    TimeZoneId = _zone.Text,
                    BackupEnabled = _backupEnabled.Checked,
                    BackupDirectory = _backupDirectory.Text,
                    BackupTime = _backupTime.Text,
                    BackupRetentionDays = (int)_retention.Value
                }, CancellationToken.None);
                return true;
            case 5:
                if (!string.Equals(_adminPassword.Text, _adminRepeat.Text, StringComparison.Ordinal))
                {
                    _message.Text = "Şifre tekrarı eşleşmiyor.";
                    return false;
                }

                var accounts = RequireData().GetRequiredService<IAccountService>();
                if (!await accounts.HasAnyUserAsync(CancellationToken.None))
                {
                    var created = await accounts.CreateInitialAdminAsync(new InitialAdminModel
                    {
                        Username = _adminUser.Text,
                        FullName = _adminName.Text,
                        Password = _adminPassword.Text
                    }, CancellationToken.None);
                    if (!created.Succeeded)
                    {
                        _message.Text = created.Message ?? "Yönetici oluşturulamadı.";
                        return false;
                    }
                }

                return true;
            case 6:
                var report = await RequireData().GetRequiredService<IInstallationReadinessService>().EvaluateAsync(CancellationToken.None);
                _complete = report.IsComplete;
                if (!_complete)
                {
                    _message.Text = "Kurulum tamamlanamadı. Hata düzeyindeki kontroller giderilmelidir.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_connection))
                    return false;
                _store.Save(_connection);
                return true;
            default:
                return true;
        }
    }

    private void ShowStep()
    {
        _body.Controls.Clear();
        _back.Enabled = _step > 0;
        _next.Text = _step == 7 ? "Bitir" : "İleri";
        switch (_step)
        {
            case 0:
                _title.Text = "Hoş Geldiniz";
                _body.Controls.Add(Note("UnAccess kurulumu SQL Server bağlantısı, hastane bilgisi, yedekleme ayarı ve ilk yönetici hesabını toplar. Bağlantı dizesi kaynak koduna yazılmaz."));
                break;
            case 1:
                _title.Text = "SQL Server Bağlantısı";
                var sql = Grid(6);
                Add(sql, 0, "Sunucu", _server);
                Add(sql, 1, "Instance", _instance);
                Add(sql, 2, "Veritabanı", _database);
                Add(sql, 3, "Kimlik doğrulama", _auth);
                Add(sql, 4, "Kullanıcı adı", _user);
                Add(sql, 5, "Parola", _sqlPassword);
                var test = new Button { Text = "BAĞLANTIYI TEST ET", Width = 180, Height = 28 };
                test.Click += async (_, _) =>
                {
                    var result = await _probe.TestAsync(Endpoint(), CancellationToken.None);
                    _message.Text = result.Message;
                    if (result.Succeeded)
                    {
                        _connection = _probe.BuildConnectionString(Endpoint());
                        ReplaceData();
                    }
                };
                var host = new Panel { Dock = DockStyle.Fill };
                sql.Dock = DockStyle.Top;
                sql.Height = 220;
                test.Location = new Point(0, 228);
                host.Controls.Add(test);
                host.Controls.Add(sql);
                _body.Controls.Add(host);
                break;
            case 2:
                _title.Text = "Veritabanı Kontrolü";
                var apply = new Button { Text = "Şemayı uygula", Width = 140, Height = 28 };
                var schema = new Label { Dock = DockStyle.Top, Height = 80, Text = "Şema henüz kontrol edilmedi." };
                apply.Click += async (_, _) =>
                {
                    try
                    {
                        await RequireData().GetRequiredService<ISetupService>().ApplySchemaAsync(CancellationToken.None);
                        var current = await RequireData().GetRequiredService<IDatabaseHealthService>().GetAsync(CancellationToken.None);
                        schema.Text = current.ConnectionText + " / " + current.MigrationText + " / " + current.ModelText;
                        _message.Text = current.PendingMigrations ? "Bekleyen migration vardı, şema uygulandı. Yeniden kontrol edin." : "Şema hazır.";
                    }
                    catch (Exception exception)
                    {
                        _message.Text = exception is UnAccessException known ? known.UserMessage : BackupMessages.Unexpected;
                    }
                };
                _body.Controls.Add(apply);
                _body.Controls.Add(schema);
                break;
            case 3:
                _title.Text = "Hastane Bilgileri";
                var hospital = Grid(2);
                Add(hospital, 0, "Hastane adı", _hospital);
                Add(hospital, 1, "Saat dilimi", _zone);
                hospital.Dock = DockStyle.Top;
                hospital.Height = 80;
                _body.Controls.Add(hospital);
                break;
            case 4:
                _title.Text = "Yedekleme Ayarları";
                var backup = Grid(4);
                Add(backup, 0, "Klasör", _backupDirectory);
                Add(backup, 1, "Saat", _backupTime);
                Add(backup, 2, "Saklama (gün)", _retention);
                var probe = new Button { Text = "Klasörü doğrula", Width = 140, Height = 28 };
                probe.Click += async (_, _) =>
                {
                    var result = await RequireData().GetRequiredService<IBackupService>().ProbeDirectoryAsync(_backupDirectory.Text, CancellationToken.None);
                    _message.Text = result.Message;
                };
                var backupHost = new Panel { Dock = DockStyle.Fill };
                backup.Dock = DockStyle.Top;
                backup.Height = 150;
                _backupEnabled.Dock = DockStyle.Top;
                probe.Location = new Point(0, 180);
                backupHost.Controls.Add(probe);
                backupHost.Controls.Add(backup);
                backupHost.Controls.Add(_backupEnabled);
                _body.Controls.Add(backupHost);
                break;
            case 5:
                _title.Text = "İlk Yönetici";
                var admin = Grid(4);
                Add(admin, 0, "Kullanıcı adı", _adminUser);
                Add(admin, 1, "Ad soyad", _adminName);
                Add(admin, 2, "Şifre", _adminPassword);
                Add(admin, 3, "Şifre tekrar", _adminRepeat);
                admin.Dock = DockStyle.Top;
                admin.Height = 160;
                _body.Controls.Add(admin);
                break;
            case 6:
                _title.Text = "Sistem Kontrolü";
                var list = new Label { Dock = DockStyle.Fill, Text = "İleri ile kurulum kontrolü çalışır." };
                _body.Controls.Add(list);
                break;
            default:
                _title.Text = _complete ? "Tamamlandı" : "Kurulum tamamlanamadı";
                _body.Controls.Add(Note(_complete
                    ? "Kurulum tamamlandı. Korunan bağlantı ayarı kaydedildi."
                    : "Kurulum tamamlanamadı. Hata düzeyindeki maddeler giderilmeden bu adım bitirilemez."));
                _next.Enabled = _complete;
                break;
        }
    }

    private SqlEndpoint Endpoint() => new()
    {
        Server = _server.Text,
        Instance = _instance.Text,
        Database = _database.Text,
        IntegratedSecurity = _auth.SelectedIndex == 1,
        UserName = _user.Text,
        Password = _sqlPassword.Text
    };

    private void ReplaceData()
    {
        _data?.Dispose();
        _data = null;
        if (!string.IsNullOrWhiteSpace(_connection))
            _data = _factory(_connection);
    }

    private ServiceProvider RequireData()
    {
        if (_data == null)
            ReplaceData();
        if (_data == null)
            throw new UnAccessException("SQL Server bağlantısı kurulamadı.");
        return _data;
    }

    private static Label Note(string text) => new() { Text = text, Dock = DockStyle.Fill };

    private static TableLayoutPanel Grid(int rows)
    {
        var grid = new TableLayoutPanel { ColumnCount = 2, RowCount = rows };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private static void Add(TableLayoutPanel grid, int row, string label, Control editor)
    {
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        editor.Dock = DockStyle.Fill;
        grid.Controls.Add(editor, 1, row);
    }
}
