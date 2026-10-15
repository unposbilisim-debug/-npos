using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class BackupControl : ScreenControl
{
    public BackupControl(IServiceScopeFactory scopes, ILogger<BackupControl> logger)
        : base(scopes, logger)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        table.Controls.Add(Caption("Son Yedek"), 0, 0);
        table.Controls.Add(Value("—"), 1, 0);
        table.Controls.Add(Caption("Yedekleme Durumu"), 0, 1);
        table.Controls.Add(Value("Yapılandırılmadı"), 1, 1);
        var note = new Label
        {
            Text = "Yedekleme modülü henüz yapılandırılmadı.",
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0)
        };
        table.Controls.Add(note, 0, 2);
        table.SetColumnSpan(note, 2);
        Controls.Add(table);
        SetStatus("Yedekleme yapılandırılmadı");
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 6, 8, 0) };

    private static Label Value(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
}
