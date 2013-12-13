namespace ServiceControl.EndpointPlugin.BusinessMonitoring
{
    using Heartbeats;
    using Messages.Heartbeats;
    using Operations.PerformanceCounters;

    class EndpointSLAHeartbeatProvider : HeartbeatInfoProvider
    {
        public PerformanceCounterCapturer PerformanceCounterCapturer { get; set; }

        public override void HeartbeatExecuted(EndpointHeartbeat heartbeat)
        {
            //heartbeat.Configuration.Add("Endpoint.SLA", Configure.Instance.EndpointSLA().ToString());
            //heartbeat.PerformanceData.Add("CriticalTime", PerformanceCounterCapturer.GetCollectedData("CriticalTime"));
        }
    }
}