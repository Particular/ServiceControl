// I don't think we need this, now that the dependency on ServiceControl.EndpointPlugin has been removed.

//namespace ServiceControl.Operations.Heartbeats
//{
//    using EndpointPlugin.Heartbeats;
//    using NServiceBus;

//    public class DisableLocalHeartbeats : INeedInitialization
//    {
//        public void Init()
//        {
//            Configure.Features.Disable<Heartbeats>(); //avoid sending heartbeats to our self
//        }
//    }
//}