namespace Particular.Operations.Heartbeats.Api
{
    using System.Threading.Tasks;

    public interface IProcessHeartbeats
    {
        Task Handle(RegisterEndpointStartup endpointStartup);
        Task Handle(EndpointHeartbeat heartbeat);
    }
}