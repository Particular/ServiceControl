namespace ServiceBus.Management.Infrastructure.Heartbeats
{
    using NServiceBus;

    public class DisableLocalHeartbeats:INeedInitialization
    {
        public void Init()
        {
            //Configure.Features.Disable<ServiceControl.EndpointPlugins.Heartbeat.Heartbeats>(); //avoid sending heartbeats to our self
        }
    }
}