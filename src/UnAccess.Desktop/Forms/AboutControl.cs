using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Desktop.Ui;

namespace UnAccess.Desktop.Forms;

internal sealed class AboutControl : ScreenControl
{
    public AboutControl(IServiceScopeFactory scopes, ILogger<AboutControl> logger)
        : base(scopes, logger)
    {
        var title = new Label { Text = "UnAccess", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(8, 12, 8, 4) };
        var version = new Label { AutoSize = true, Margin = new Padding(8, 4, 8, 0), Text = "Sürüm —" };
        var build = new Label { AutoSize = true, Margin = new Padding(8, 4, 8, 0), Text = "Derleme —" };
        var environment = new Label { AutoSize = true, Margin = new Padding(8, 4, 8, 0), Text = "Ortam —" };
        var update = new Label { AutoSize = true, Margin = new Padding(8, 12, 8, 0), Text = "—" };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(title);
        panel.Controls.Add(version);
        panel.Controls.Add(build);
        panel.Controls.Add(environment);
        panel.Controls.Add(update);
        Controls.Add(panel);
        Load += async (_, _) =>
        {
            await RunAsync(async services =>
            {
                var info = services.GetRequiredService<IApplicationVersionProvider>();
                var check = await services.GetRequiredService<IUpdateCheckService>().CheckAsync(Cancellation);
                version.Text = "Sürüm " + info.Version;
                build.Text = "Derleme " + info.InformationalVersion;
                environment.Text = "Ortam " + info.EnvironmentName;
                update.Text = check.Message;
            });
            SetStatus("Hakkında");
        };
    }
}
