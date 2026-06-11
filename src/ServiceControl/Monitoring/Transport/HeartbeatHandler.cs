namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.Persistence;

    [Handler]
    class HeartbeatHandler(IEndpointInstanceMonitoring monitor) : IHandleMessages<EndpointHeartbeat>
    {
        public Task Handle(EndpointHeartbeat message, IMessageHandlerContext context)
        {
            var endpointInstanceId = new EndpointInstanceId(message.EndpointName, message.Host, message.HostId);

            monitor.RecordHeartbeat(endpointInstanceId, message.ExecutedAt);

            return Task.CompletedTask;
        }
    }
}