namespace ServiceBus.Management.Operations.Heartbeats
{
    using NServiceBus;
    using ServiceControl.EndpointPlugin.Operations.Heartbeats;

    public class DisableLocalHeartbeats : INeedInitialization
    {
        public void Init()
        {
            Configure.Features.Disable<Heartbeats>(); //avoid sending heartbeats to our self
        }
    }
}