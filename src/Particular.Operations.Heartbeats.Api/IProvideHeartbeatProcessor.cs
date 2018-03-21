namespace Particular.Operations.Heartbeats.Api
{
    public interface IProvideHeartbeatProcessor
    {
        IProcessHeartbeats ProcessHeartbeats { get; }
    }
}