namespace ServiceControl.EndpointPlugin.Heartbeats
{
    using Messages.Heartbeats;

    abstract class HeartbeatInfoProvider : IHeartbeatInfoProvider
    {
        public abstract void HeartbeatExecuted(EndpointHeartbeat heartbeat);
    }
}