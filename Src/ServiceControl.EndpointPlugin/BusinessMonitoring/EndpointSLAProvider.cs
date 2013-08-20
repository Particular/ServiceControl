namespace ServiceControl.EndpointPlugin.BusinessMonitoring
{
    using Heartbeats;
    using NServiceBus;

    //todo: this provider is only relevant to execure on startup
    public class EndpointSLAProvider:HeartbeatInfoProvider
    {
        public override void HeartbeatExecuted(EndpointHeartbeat heartbeat)
        {
            heartbeat.Configuration.Add("Endpoint.SLA", Configure.Instance.EndpointSLA().ToString());
        }
    }
}