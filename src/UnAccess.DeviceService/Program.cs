using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnAccess.Core.Abstractions;
using UnAccess.Data;
using UnAccess.Data.Infrastructure;
using UnAccess.DeviceService;
using UnAccess.Devices;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration["UnAccess:Environment"] = builder.Environment.EnvironmentName;
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "UnAccess Device Service";
});
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new SanitizingConsoleLoggerProvider(LogLevel.Information));
builder.Services.AddSingleton<SimulatorControllerRegistry>();
builder.Services.AddSingleton<IControllerClientFactory, SimulatorClientFactory>();
builder.Services.AddUnAccessData(builder.Configuration);
builder.Services.AddSingleton<ILoggerProvider>(services =>
    new SanitizingFileLoggerProvider(services.GetRequiredService<IAppPathProvider>(), LogLevel.Information));
builder.Services.AddHostedService<DeviceSyncWorker>();
builder.Services.AddHostedService<BackupSchedulerWorker>();

var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("UnAccess.DeviceService");
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
}
AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    if (args.ExceptionObject is Exception exception)
        logger.LogCritical(exception, "Beklenmeyen servis hatası. Makine {Machine}", Environment.MachineName);
};
TaskScheduler.UnobservedTaskException += (_, args) =>
{
    logger.LogError(args.Exception, "Gözlemlenmeyen servis görevi. Makine {Machine}", Environment.MachineName);
    args.SetObserved();
};

await host.RunAsync();
