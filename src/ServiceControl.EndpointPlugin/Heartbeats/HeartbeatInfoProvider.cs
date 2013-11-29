namespace ServiceControl.EndpointPlugin.Heartbeats
{
  
    using Plugin.Heartbeats;
    using Plugin.Heartbeats.Messages;

    abstract class HeartbeatInfoProvider : IHeartbeatInfoProvider
    {
        public abstract void HeartbeatExecuted(EndpointHeartbeat heartbeat);
    }
}