namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.Persistence;

    class HeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public HeartbeatHandler(IEndpointInstanceMonitoring monitor)
        {
            this.monitor = monitor;
        }

        public Task Handle(EndpointHeartbeat message, IMessageHandlerContext context)
        {
            var endpointInstanceId = new EndpointInstanceId(message.EndpointName, message.Host, message.HostId);

            monitor.RecordHeartbeat(endpointInstanceId, message.ExecutedAt);

            return Task.CompletedTask;
        }

        IEndpointInstanceMonitoring monitor;
    }
}