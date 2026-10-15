using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Backup;
using UnAccess.Core.Security;
using UnAccess.Data;
using UnAccess.Data.Infrastructure;
using UnAccess.Data.Services;
using UnAccess.Desktop.Forms;
using UnAccess.Desktop.Passage;
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
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UnAccess:Environment"] = environment
            })
            .Build();

        var minimumLevel = Enum.TryParse<LogLevel>(configuration["Logging:MinimumLevel"], true, out var parsed)
            ? parsed
            : LogLevel.Information;
        var paths = new AppPathProvider();
        var store = new FileConnectionConfigurationStore(paths, new DpapiSecretProtector());
        var connectionString = configuration.GetConnectionString("UnAccess");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var stored = store.Read();
            if (stored.Success)
                connectionString = stored.ConnectionString;
        }

        var production = string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(connectionString) && production)
        {
            using var wizard = new SetupWizardForm(
                new ConnectionProbe(LoggerFactory.Create(builder => builder.AddProvider(new SanitizingConsoleLoggerProvider(minimumLevel))).CreateLogger<ConnectionProbe>()),
                value => BuildProvider(configuration, environment, minimumLevel, paths, value),
                store);
            if (wizard.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(wizard.ConnectionString))
                return;
            connectionString = wizard.ConnectionString;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration(configuration, environment, connectionString));
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(minimumLevel);
            builder.AddProvider(new SanitizingFileLoggerProvider(paths, minimumLevel));
            builder.AddProvider(new SanitizingConsoleLoggerProvider(minimumLevel));
        });

        var dataConfigured = !string.IsNullOrWhiteSpace(connectionString);
        if (dataConfigured)
        {
            services.AddUnAccessData(BuildConfiguration(configuration, environment, connectionString));
            services.AddTransient<ISimulatedPassageService, SimulatorPassageDecorator>();
        }

        services.AddSingleton<IAccessController, DahuaSimulatorController>();
        services.AddTransient<MainForm>();

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("UnAccess");
        var version = typeof(BackupMessages).Assembly.GetName().Version?.ToString() ?? "";
        Application.ThreadException += (_, args) =>
        {
            logger.LogError(args.Exception, "Beklenmeyen arayüz hatası. Sürüm {Version} Makine {Machine} Bağlam {Context}", version, Environment.MachineName, "Desktop");
            Ui.UiErrors.Show(logger, null, args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                logger.LogCritical(exception, "Yakalanmayan hata. Sürüm {Version} Makine {Machine} Bağlam {Context}", version, Environment.MachineName, "Desktop");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "Gözlemlenmeyen görev hatası. Sürüm {Version} Makine {Machine} Bağlam {Context}", version, Environment.MachineName, "Desktop");
            args.SetObserved();
        };

        if (!dataConfigured)
        {
            logger.LogWarning(Core.Exceptions.DataMessages.MissingConnectionString);
            MessageBox.Show(
                "Veritabanı bağlantısı tanımlı değil. Uygulama açılamadı.",
                "UnAccess",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
                .InitializeAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
            var current = provider.GetRequiredService<ICurrentUser>();
            if (!accounts.HasAnyUserAsync(CancellationToken.None).GetAwaiter().GetResult())
            {
                using var first = new FirstAdminForm(accounts);
                if (first.ShowDialog() != DialogResult.OK)
                    return;
            }

            if (!current.IsAuthenticated)
            {
                using var login = new LoginForm(accounts);
                if (login.ShowDialog() != DialogResult.OK)
                    return;
            }

            if (current.MustChangePassword)
            {
                using var change = new ChangePasswordForm(accounts, required: true);
                if (change.ShowDialog() != DialogResult.OK)
                    return;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Veritabanı başlatılamadı.");
            MessageBox.Show(
                "Veritabanına bağlanılamadı veya şema güncellenemedi. Ayrıntı günlük dosyasına yazıldı.",
                "UnAccess",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Application.Run(provider.GetRequiredService<MainForm>());
    }

    private static IConfiguration BuildConfiguration(IConfiguration configuration, string environment, string? connectionString)
    {
        var values = new Dictionary<string, string?>
        {
            ["UnAccess:Environment"] = environment
        };
        if (!string.IsNullOrWhiteSpace(connectionString))
            values["ConnectionStrings:UnAccess"] = connectionString;
        return new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration, string environment, LogLevel minimumLevel, IAppPathProvider paths, string connectionString)
    {
        var built = BuildConfiguration(configuration, environment, connectionString);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(built);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(minimumLevel);
            builder.AddProvider(new SanitizingFileLoggerProvider(paths, minimumLevel));
            builder.AddProvider(new SanitizingConsoleLoggerProvider(minimumLevel));
        });
        services.AddUnAccessData(built);
        return services.BuildServiceProvider();
    }
}
