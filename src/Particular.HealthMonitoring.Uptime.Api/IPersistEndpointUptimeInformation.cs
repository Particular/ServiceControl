namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System.Threading.Tasks;

    public interface IPersistEndpointUptimeInformation
    {
        Task<EndpointUptimeInfo[]>Load();
        Task Store(EndpointUptimeInfo info);
    }
}