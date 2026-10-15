using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class SystemStatusControl : ScreenControl
{
    private readonly Label _database = Line();
    private readonly Label _service = Line();
    private readonly Label _controllers = Line();
    private readonly Label _pending = Line();
    private readonly Label _failed = Line();
    private readonly Label _dead = Line();
    private readonly Label _lastEvent = Line();
    private readonly Label _migration = Line();
    private readonly Label _model = Line();
    private readonly Label _backup = Line();
    private readonly Label _backupSuccess = Line();
    private readonly Label _backupFailure = Line();
    private readonly Label _backupFolder = Line();
    private readonly Label _disk = Line();

    public SystemStatusControl(IServiceScopeFactory scopes, ILogger<SystemStatusControl> logger)
        : base(scopes, logger)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Add(table, 0, "SQL Server", _database);
        Add(table, 1, "DeviceService", _service);
        Add(table, 2, "Kontrolörler", _controllers);
        Add(table, 3, "Bekleyen iş", _pending);
        Add(table, 4, "Başarısız iş", _failed);
        Add(table, 5, "Teslim edilemeyen", _dead);
        Add(table, 6, "Son geçiş kaydı", _lastEvent);
        Add(table, 7, "Migration", _migration);
        Add(table, 8, "Model", _model);
        Add(table, 9, "Yedekleme", _backup);
        Add(table, 10, "Son başarılı yedek", _backupSuccess);
        Add(table, 11, "Son başarısız yedek", _backupFailure);
        Add(table, 12, "Yedek klasörü", _backupFolder);
        Add(table, 13, "Disk boş alanı", _disk);
        var refresh = new Button { Text = "Yenile", Width = 96, Height = 28, Margin = new Padding(8, 8, 0, 0) };
        refresh.Click += async (_, _) => await ReloadAsync();
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44 };
        bar.Controls.Add(refresh);
        Controls.Add(table);
        Controls.Add(bar);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var ok = await RunAsync(async services =>
        {
            var health = await services.GetRequiredService<ISystemHealthService>().GetAsync(Cancellation);
            var clock = services.GetRequiredService<IHospitalClock>();
            _database.Text = health.DatabaseText;
            var seen = health.DeviceServiceLastSeenUtc is DateTime stamp
                ? " — " + clock.ToHospitalLocal(DateTime.SpecifyKind(stamp, DateTimeKind.Utc)).ToString("dd.MM.yyyy HH:mm:ss")
                : "";
            _service.Text = health.DeviceServiceText + seen;
            _controllers.Text = $"Çevrimiçi {health.ControllersOnline} / Çevrimdışı {health.ControllersOffline} / Bilinmiyor {health.ControllersUnknown}";
            _pending.Text = health.SyncPending.ToString();
            _failed.Text = health.SyncFailed.ToString();
            _dead.Text = health.SyncDeadLetter.ToString();
            _lastEvent.Text = health.LastAccessEventUtc is DateTime occurred
                ? clock.ToHospitalLocal(DateTime.SpecifyKind(occurred, DateTimeKind.Utc)).ToString("dd.MM.yyyy HH:mm:ss")
                : "—";
            _migration.Text = health.MigrationText;
            _model.Text = health.ModelText;
            _backup.Text = health.BackupStatusText;
            _backupSuccess.Text = Stamp(clock, health.LastSuccessfulBackupUtc);
            _backupFailure.Text = Stamp(clock, health.LastFailedBackupUtc);
            _backupFolder.Text = string.IsNullOrWhiteSpace(health.BackupDirectory)
                ? health.BackupFolderText
                : health.BackupDirectory + " — " + health.BackupFolderText;
            _disk.Text = health.DiskSpaceText;
        });
        if (ok)
            SetStatus("Sistem durumu");
    }

    private static void Add(TableLayoutPanel table, int row, string caption, Label value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 6, 8, 0) }, 0, row);
        table.Controls.Add(value, 1, row);
    }

    private static string Stamp(IHospitalClock clock, DateTime? utc) =>
        utc is DateTime value
            ? clock.ToHospitalLocal(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToString("dd.MM.yyyy HH:mm:ss")
            : "—";

    private static Label Line() => new() { AutoSize = true, Margin = new Padding(0, 6, 0, 0), Text = "—" };
}
