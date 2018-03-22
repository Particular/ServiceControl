namespace Particular.HealthMonitoring.Uptime.Api
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPersistEndpointUptimeInformation
    {
        Task<IHeartbeatEvent[]> Load();
        Task Store(IEnumerable<IHeartbeatEvent> events);
    }
}