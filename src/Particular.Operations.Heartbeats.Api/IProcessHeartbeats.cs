namespace Particular.Operations.Heartbeats.Api
{
    public interface IProcessHeartbeats
    {
        void Handle(RegisterEndpointStartup endpointStartup);
        void Handle(EndpointHeartbeat heartbeat);
    }
}