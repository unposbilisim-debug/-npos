using Microsoft.Extensions.Hosting;
using UnAccess.Data;
using UnAccess.DeviceService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<SimulatorControllerRegistry>();
builder.Services.AddSingleton<IControllerClientFactory, SimulatorClientFactory>();
builder.Services.AddUnAccessData(builder.Configuration);
builder.Services.AddHostedService<DeviceSyncWorker>();

var host = builder.Build();
await host.RunAsync();
