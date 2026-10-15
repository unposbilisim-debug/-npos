using UnAccess.Core.Abstractions;

namespace UnAccess.Data.Infrastructure;

internal sealed class SystemUtcClock : IUtcClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
