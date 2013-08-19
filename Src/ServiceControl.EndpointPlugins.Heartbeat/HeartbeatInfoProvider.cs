namespace ServiceControl.EndpointPlugins.Heartbeat
{
    //todo: better name?
    public abstract class HeartbeatInfoProvider :IHeartbeatInfoProvider
    {
        public abstract void HeartbeatExecuted(EndpointHeartbeat heartbeat);
    }

    //we need this for now until we can patch builder.BuildAll to support abstract base classes
    public interface IHeartbeatInfoProvider
    {
        void HeartbeatExecuted(EndpointHeartbeat heartbeat);
    }
}