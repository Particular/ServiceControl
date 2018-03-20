namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System.Threading.Tasks;

    public interface IPersistEndpointUptimeInformation
    {
        Task<IHeartbeatEvent[]> Load();
        Task Store(IHeartbeatEvent @event);
    }
}