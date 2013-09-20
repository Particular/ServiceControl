namespace ServiceControl.EndpointPlugin.Heartbeats
{
    using Messages.Heartbeats;

    //todo: better name?
    public abstract class HeartbeatInfoProvider : IHeartbeatInfoProvider
    {
        public abstract void HeartbeatExecuted(EndpointHeartbeat heartbeat);
    }
}