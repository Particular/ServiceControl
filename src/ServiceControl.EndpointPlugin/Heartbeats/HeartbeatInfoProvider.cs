namespace ServiceControl.EndpointPlugin.Heartbeats
{
    using Messages.Heartbeats;
    using Plugin.Heartbeats;

    abstract class HeartbeatInfoProvider : IHeartbeatInfoProvider
    {
        public abstract void HeartbeatExecuted(EndpointHeartbeat heartbeat);
    }
}