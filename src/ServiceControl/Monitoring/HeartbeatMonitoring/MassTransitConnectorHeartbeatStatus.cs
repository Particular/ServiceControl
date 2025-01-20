#nullable enable
namespace ServiceControl.Monitoring.HeartbeatMonitoring;

using Connector.MassTransit;

public class MassTransitConnectorHeartbeatStatus
{
    public MassTransitConnectorHeartbeat? LastHeartbeat { get; private set; }

    public void Update(MassTransitConnectorHeartbeat lastHeartbeat) => LastHeartbeat = lastHeartbeat;
}