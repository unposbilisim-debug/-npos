using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Models;
using UnAccess.Core.Security;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class SystemSettingsControl : ScreenControl
{
    private readonly TableLayoutPanel _fields = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
    private readonly Dictionary<string, TextBox> _boxes = new();

    public SystemSettingsControl(IServiceScopeFactory scopes, ILogger<SystemSettingsControl> logger)
        : base(scopes, logger)
    {
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var save = new Button { Text = "Kaydet", Width = 96, Height = 28, Margin = new Padding(0, 8, 8, 0) };
        var refresh = new Button { Text = "Yenile", Width = 96, Height = 28, Margin = new Padding(0, 8, 0, 0) };
        save.Click += async (_, _) => await SaveAsync();
        refresh.Click += async (_, _) => await ReloadAsync();
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44 };
        bar.Controls.Add(save);
        bar.Controls.Add(refresh);
        Controls.Add(_fields);
        Controls.Add(bar);
        Load += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<SystemSettingItem>? items = null;
        var ok = await RunAsync(async services =>
        {
            items = await services.GetRequiredService<ISystemSettingService>().ListAsync(Cancellation);
        });
        if (!ok || items == null || IsDisposed)
            return;

        _fields.Controls.Clear();
        _boxes.Clear();
        var row = 0;
        foreach (var item in items)
        {
            _fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            _fields.Controls.Add(new Label { Text = Caption(item.Key), AutoSize = true, Margin = new Padding(0, 8, 8, 0) }, 0, row);
            var box = new TextBox { Text = item.Value, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 0) };
            _boxes[item.Key] = box;
            _fields.Controls.Add(box, 1, row);
            row++;
        }

        SetStatus("Ayarlar");
    }

    private async Task SaveAsync()
    {
        var items = _boxes.Select(pair => new SystemSettingItem { Key = pair.Key, Value = pair.Value.Text }).ToList();
        var ok = await RunAsync(async services =>
        {
            await services.GetRequiredService<ISystemSettingService>().SaveAsync(items, Cancellation);
        });
        if (ok)
        {
            SetStatus("Ayarlar kaydedildi");
            await ReloadAsync();
        }
    }

    private static string Caption(string key) => key switch
    {
        SettingKeys.HospitalName => "Hastane Adı",
        SettingKeys.TimeZone => "Saat Dilimi",
        SettingKeys.LoginMaxFailedAttempts => "Hatalı giriş sınırı",
        SettingKeys.LoginLockoutMinutes => "Kilitleme süresi (dakika)",
        SettingKeys.PasswordMinLength => "En az şifre uzunluğu",
        SettingKeys.DeviceSyncBatchSize => "Senkronizasyon paket boyutu",
        SettingKeys.DeviceSyncStaleProcessingMinutes => "Yarım kalan iş süresi (dakika)",
        SettingKeys.ServiceHeartbeatIntervalSeconds => "Servis nabız aralığı (saniye)",
        SettingKeys.ServiceHeartbeatStaleSeconds => "Servis yanıt süresi (saniye)",
        _ => key
    };
}
