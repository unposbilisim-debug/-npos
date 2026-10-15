using UnAccess.Core.Models;

namespace UnAccess.Core.Abstractions;

public interface IHospitalStructureService
{
    Task<IReadOnlyList<BuildingListItem>> ListBuildingsAsync(CancellationToken cancellationToken);
    Task<BuildingListItem?> GetBuildingAsync(int id, CancellationToken cancellationToken);
    Task<BuildingListItem> CreateBuildingAsync(BuildingEditModel model, CancellationToken cancellationToken);
    Task<BuildingListItem> UpdateBuildingAsync(int id, BuildingEditModel model, CancellationToken cancellationToken);

    Task<IReadOnlyList<FloorListItem>> ListFloorsAsync(int? buildingId, CancellationToken cancellationToken);
    Task<FloorListItem?> GetFloorAsync(int id, CancellationToken cancellationToken);
    Task<FloorListItem> CreateFloorAsync(FloorEditModel model, CancellationToken cancellationToken);
    Task<FloorListItem> UpdateFloorAsync(int id, FloorEditModel model, CancellationToken cancellationToken);

    Task<IReadOnlyList<ZoneListItem>> ListZonesAsync(int? buildingId, int? floorId, CancellationToken cancellationToken);
    Task<ZoneListItem?> GetZoneAsync(int id, CancellationToken cancellationToken);
    Task<ZoneListItem> CreateZoneAsync(ZoneEditModel model, CancellationToken cancellationToken);
    Task<ZoneListItem> UpdateZoneAsync(int id, ZoneEditModel model, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoorListItem>> ListDoorsAsync(int? zoneId, CancellationToken cancellationToken);
    Task<DoorEditModel?> GetDoorAsync(int id, CancellationToken cancellationToken);
    Task<DoorListItem> CreateDoorAsync(DoorEditModel model, CancellationToken cancellationToken);
    Task<DoorListItem> UpdateDoorAsync(int id, DoorEditModel model, CancellationToken cancellationToken);

    Task<IReadOnlyList<LookupItem>> GetBuildingOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FloorOption>> GetFloorOptionsAsync(int? buildingId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ZoneOption>> GetZoneOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ControllerOption>> GetControllerOptionsAsync(CancellationToken cancellationToken);
}
