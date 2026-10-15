using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Data;
using UnAccess.Desktop.Forms;
using UnAccess.Desktop.Logging;
using UnAccess.Devices;
using UnAccess.Simulator;

namespace UnAccess.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var culture = new CultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        var environment = Environment.GetEnvironmentVariable("UNACCESS_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        var appDataSettings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnAccess",
            "appsettings.json");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddJsonFile(appDataSettings, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var minimumLevel = Enum.TryParse<LogLevel>(configuration["Logging:MinimumLevel"], true, out var parsed)
            ? parsed
            : LogLevel.Information;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(minimumLevel);
            builder.AddProvider(new FileLoggerProvider(minimumLevel));
        });

        var connectionString = configuration.GetConnectionString("UnAccess");
        var dataConfigured = !string.IsNullOrWhiteSpace(connectionString);
        if (dataConfigured)
            services.AddUnAccessData(configuration);

        services.AddSingleton<IAccessController, DahuaSimulatorController>();
        services.AddTransient<MainForm>();

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("UnAccess");
        Application.ThreadException += (_, args) => Ui.UiErrors.Show(logger, null, args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                logger.LogCritical(exception, "Yakalanmayan hata");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "Gözlemlenmeyen görev hatası");
            args.SetObserved();
        };

        if (!dataConfigured)
            logger.LogWarning(Core.Exceptions.DataMessages.MissingConnectionString);
        else
        {
            try
            {
                using var scope = provider.CreateScope();
                scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
                    .InitializeAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Veritabanı başlatılamadı.");
                MessageBox.Show(
                    "Veritabanına bağlanılamadı veya şema güncellenemedi. Ayrıntı günlük dosyasına yazıldı.",
                    "UnAccess",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        Application.Run(provider.GetRequiredService<MainForm>());
    }
}
