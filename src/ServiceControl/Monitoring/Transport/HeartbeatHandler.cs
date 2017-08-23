namespace ServiceControl.Monitoring
{
    using NServiceBus;
    using ServiceControl.Plugin.Heartbeat.Messages;

    class HeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public void Handle(EndpointHeartbeat message)
        {
            var endpointInstanceId = new EndpointInstanceId(message.EndpointName, message.Host, message.HostId);

            monitor.RecordHeartbeat(endpointInstanceId, message.ExecutedAt);
        }

        public HeartbeatHandler(EndpointInstanceMonitoring monitor)
        {
            this.monitor = monitor;
        }

        private EndpointInstanceMonitoring monitor;
    }
}