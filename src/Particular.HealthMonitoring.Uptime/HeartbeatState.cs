namespace Particular.HealthMonitoring.Uptime
{
    using System.Collections.Generic;
    using Particular.HealthMonitoring.Uptime.Api;

    class HeartbeatState
    {
        public List<IHeartbeatEvent> State { get; set; }
    }
}