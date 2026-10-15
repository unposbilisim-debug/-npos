namespace UnAccess.Core.Abstractions;

public interface IUtcClock
{
    DateTime UtcNow { get; }
}
