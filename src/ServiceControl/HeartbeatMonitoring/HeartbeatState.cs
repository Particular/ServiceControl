namespace ServiceControl.Monitoring
{
    using System.Collections.Generic;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    class HeartbeatState
    {
        public List<IHeartbeatEvent> State { get; set; }
    }
}