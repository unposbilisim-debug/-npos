using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace UnAccess.Data.Infrastructure;

public sealed class UtcMaterializationInterceptor : IMaterializationInterceptor
{
    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        Apply(entity, materializationData.EntityType);
        return entity;
    }

    public static void Apply(object entity, IEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(entityType);

        foreach (var property in entityType.GetProperties())
        {
            var propertyInfo = property.PropertyInfo;
            if (propertyInfo == null)
                continue;

            if (property.ClrType == typeof(DateTime))
            {
                if (propertyInfo.GetValue(entity) is DateTime value)
                    propertyInfo.SetValue(entity, DateTime.SpecifyKind(value, DateTimeKind.Utc));
            }
            else if (property.ClrType == typeof(DateTime?) && propertyInfo.GetValue(entity) is DateTime nullableValue)
            {
                propertyInfo.SetValue(entity, DateTime.SpecifyKind(nullableValue, DateTimeKind.Utc));
            }
        }
    }
}
