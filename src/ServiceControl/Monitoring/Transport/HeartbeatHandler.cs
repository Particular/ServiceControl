namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Plugin.Heartbeat.Messages;

    class HeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public Task Handle(EndpointHeartbeat message, IMessageHandlerContext context)
        {
            var endpointInstanceId = new EndpointInstanceId(message.EndpointName, message.Host, message.HostId);

            monitor.RecordHeartbeat(endpointInstanceId, message.ExecutedAt);

            return Task.FromResult(0);
        }

        public HeartbeatHandler(EndpointInstanceMonitoring monitor)
        {
            this.monitor = monitor;
        }

        private EndpointInstanceMonitoring monitor;
    }
}