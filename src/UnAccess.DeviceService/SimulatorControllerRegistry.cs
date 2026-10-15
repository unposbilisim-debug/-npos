using System.Collections.Concurrent;
using UnAccess.Devices;
using UnAccess.Simulator;

namespace UnAccess.DeviceService;

public interface IControllerClientFactory
{
    IAccessController Create(int controllerId);
}

public sealed class SimulatorControllerRegistry
{
    private readonly ConcurrentDictionary<int, DahuaSimulatorController> _controllers = new();

    public DahuaSimulatorController Get(int controllerId) =>
        _controllers.GetOrAdd(controllerId, _ => new DahuaSimulatorController());
}

public sealed class SimulatorClientFactory : IControllerClientFactory
{
    private readonly SimulatorControllerRegistry _registry;

    public SimulatorClientFactory(SimulatorControllerRegistry registry)
    {
        _registry = registry;
    }

    public IAccessController Create(int controllerId) => _registry.Get(controllerId);
}
