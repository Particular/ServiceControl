namespace ServiceControl.EndpointPlugins.Heartbeat.Providers
{
    using NServiceBus;

    //todo: this provider is only relevant to execure on startup
    public class EndpointSLASettingsProvider:HeartbeatInfoProvider
    {
        public override void HeartbeatExecuted(EndpointHeartbeat heartbeat)
        {
            heartbeat.Configuration.Add("Endpoint.SLA", Configure.Instance.EndpointSLA().ToString());
        }
    }
}