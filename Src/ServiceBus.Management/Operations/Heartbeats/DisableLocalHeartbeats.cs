namespace ServiceControl.Operations.Heartbeats
{
    using NServiceBus;

    public class DisableLocalHeartbeats : INeedInitialization
    {
        public void Init()
        {
            //Configure.Features.Disable<EndpointPlugin.Operations.Heartbeats.Heartbeats>(); //avoid sending heartbeats to our self
        }
    }
}