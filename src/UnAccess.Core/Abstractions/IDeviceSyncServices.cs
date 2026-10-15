using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface IDevicePermissionProjectionService
{
    Task<PersonnelDeviceProjection> ProjectPersonnelAsync(int personnelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceCardCommand>> ProjectControllerAsync(int controllerId, CancellationToken cancellationToken);
}

public interface IDeviceSyncProcessor
{
    Task RecoverStaleAsync(DateTime utcNow, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceSyncJob>> ClaimAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken);
    Task<DeviceCommand> BuildCommandAsync(DeviceSyncJob job, CancellationToken cancellationToken);
    Task CompleteAsync(long jobId, int controllerId, DateTime utcNow, CancellationToken cancellationToken);
    Task FailAsync(long jobId, int controllerId, string error, DateTime utcNow, CancellationToken cancellationToken);
}

public interface IDeviceSyncService
{
    Task<DeviceSyncSummary> GetSummaryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceSyncJobItem>> SearchAsync(DeviceSyncFilter filter, CancellationToken cancellationToken);
    Task<int> RetryFailedAsync(CancellationToken cancellationToken);
    Task RequestFullSyncAsync(int controllerId, CancellationToken cancellationToken);
}
