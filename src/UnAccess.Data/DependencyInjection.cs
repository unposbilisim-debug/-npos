using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnAccess.Core.Abstractions;
using UnAccess.Core.Access;
using UnAccess.Core.Exceptions;
using UnAccess.Data.Infrastructure;
using UnAccess.Data.Services;

namespace UnAccess.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddUnAccessData(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("UnAccess");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new UnAccessException(DataMessages.MissingConnectionString);

        services.AddDbContextFactory<UnAccessDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(UnAccessDbContext).Assembly.GetName().Name));
            options.AddInterceptors(new UtcMaterializationInterceptor());
        });

        services.AddSingleton<ICurrentUser, CurrentUserAccessor>();
        services.AddSingleton<IAccessGuard, AccessGuard>();
        services.AddSingleton<AuditWriter>();
        services.AddSingleton<IUtcClock, SystemUtcClock>();
        services.AddSingleton(DeviceSyncOptionsReader.Read(configuration));
        services.AddSingleton<IHospitalClock, HospitalClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IDeviceSyncOutbox, DeviceSyncOutbox>();
        services.AddSingleton<ITimeScheduleEvaluator, TimeScheduleEvaluator>();
        services.AddTransient<PersonnelService>();
        services.AddTransient<IPersonnelService, GuardedPersonnelService>();
        services.AddTransient<AccessDecisionService>();
        services.AddTransient<IAccessDecisionService, GuardedAccessDecisionService>();
        services.AddTransient<AccessGroupService>();
        services.AddTransient<IAccessGroupService, GuardedAccessGroupService>();
        services.AddTransient<TimeScheduleService>();
        services.AddTransient<ITimeScheduleService, GuardedTimeScheduleService>();
        services.AddTransient<PersonnelAuthorizationService>();
        services.AddTransient<IPersonnelAuthorizationService, GuardedPersonnelAuthorizationService>();
        services.AddTransient<PassageRecorder>();
        services.AddTransient<IPassageRecorder, GuardedPassageRecorder>();
        services.AddTransient<AccessEventQueryService>();
        services.AddTransient<IAccessEventQueryService, GuardedAccessEventQueryService>();
        services.AddTransient<CardService>();
        services.AddTransient<ICardService, GuardedCardService>();
        services.AddTransient<HospitalStructureService>();
        services.AddTransient<IHospitalStructureService, GuardedHospitalStructureService>();
        services.AddTransient<ControllerQueryService>();
        services.AddTransient<IControllerQueryService, GuardedControllerQueryService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<IAuditQueryService, AuditQueryService>();
        services.AddTransient<IDevicePermissionProjectionService, DevicePermissionProjectionService>();
        services.AddTransient<IDeviceSyncProcessor, DeviceSyncProcessor>();
        services.AddTransient<DeviceSyncService>();
        services.AddTransient<IDeviceSyncService, GuardedDeviceSyncService>();
        services.AddTransient<IAccountService, AccountService>();
        services.AddTransient<IUserAdminService, UserAdminService>();
        services.AddTransient<RuntimeSettingsService>();
        services.AddTransient<IRuntimeSettings>(provider => provider.GetRequiredService<RuntimeSettingsService>());
        services.AddTransient<ISystemSettingService>(provider => provider.GetRequiredService<RuntimeSettingsService>());
        services.AddTransient<IServiceMonitor, ServiceMonitor>();
        services.AddSingleton<IUnAccessEnvironment, UnAccessEnvironment>();
        services.AddSingleton<IAppPathProvider, AppPathProvider>();
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<IConnectionConfigurationStore, FileConnectionConfigurationStore>();
        services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
        services.AddSingleton<IUpdateCheckService, NotConfiguredUpdateService>();
        services.AddSingleton<IConfigurationAccessor, ConfigurationAccessor>();
        services.AddSingleton<SqlBackupGateway>();
        services.AddSingleton<IMaintenanceGate, SqlMaintenanceGate>();
        services.AddSingleton<OperationBook>();
        services.AddTransient<BackupService>();
        services.AddTransient<IBackupService>(provider => provider.GetRequiredService<BackupService>());
        services.AddTransient<ISafetyBackup>(provider => provider.GetRequiredService<BackupService>());
        services.AddTransient<IRestoreService, RestoreService>();
        services.AddTransient<IDatabaseHealthService, DatabaseHealthService>();
        services.AddTransient<ISystemHealthService, SystemHealthService>();
        services.AddTransient<IInstallationReadinessService, InstallationReadinessService>();
        services.AddTransient<IConnectionProbe, ConnectionProbe>();
        services.AddTransient<ISetupService, SetupService>();
        services.AddTransient<IReportService, ReportService>();
        services.AddTransient<DatabaseInitializer>();
        return services;
    }
}
