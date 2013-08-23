namespace ServiceBus.Management.Infrastructure.Heartbeats
{
    using NServiceBus;
    using ServiceControl.EndpointPlugin.Infrastructure.Heartbeats;

    public class DisableLocalHeartbeats : INeedInitialization
    {
        public void Init()
        {
            Configure.Features.Disable<Heartbeats>(); //avoid sending heartbeats to our self
        }
    }
}