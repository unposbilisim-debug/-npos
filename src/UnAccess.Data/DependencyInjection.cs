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

        services.AddSingleton<AuditWriter>();
        services.AddSingleton<IHospitalClock, HospitalClock>();
        services.AddSingleton<ITimeScheduleEvaluator, TimeScheduleEvaluator>();
        services.AddTransient<IPersonnelService, PersonnelService>();
        services.AddTransient<IAccessDecisionService, AccessDecisionService>();
        services.AddTransient<IAccessGroupService, AccessGroupService>();
        services.AddTransient<ITimeScheduleService, TimeScheduleService>();
        services.AddTransient<IPersonnelAuthorizationService, PersonnelAuthorizationService>();
        services.AddTransient<IPassageRecorder, PassageRecorder>();
        services.AddTransient<IAccessEventQueryService, AccessEventQueryService>();
        services.AddTransient<ICardService, CardService>();
        services.AddTransient<IHospitalStructureService, HospitalStructureService>();
        services.AddTransient<IControllerQueryService, ControllerQueryService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<DatabaseInitializer>();
        return services;
    }
}
