namespace ServiceControl.EndpointPlugin.BusinessMonitoring
{
    using Infrastructure.PerformanceCounters;
    using NServiceBus;
    using Operations.Heartbeats;

    public class EndpointSLAHeartbeatProvider : HeartbeatInfoProvider
    {
        public PerformanceCounterCapturer PerformanceCounterCapturer { get; set; }

        public override void HeartbeatExecuted(EndpointHeartbeat heartbeat)
        {
            //heartbeat.Configuration.Add("Endpoint.SLA", Configure.Instance.EndpointSLA().ToString());
            //heartbeat.PerformanceData.Add("CriticalTime", PerformanceCounterCapturer.GetCollectedData("CriticalTime"));
        }
    }
}