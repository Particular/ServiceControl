namespace ServiceControl.Monitoring
{
    using System.Collections.Generic;
    using Particular.HealthMonitoring.Uptime.Api;

    class HeartbeatState
    {
        public List<IHeartbeatEvent> State { get; set; }
    }
}