namespace ServiceControl.HeartbeatMonitoring
{
    using Contracts.Operations;
    using NServiceBus;

    public class EndpointHeartbeatReceivedHandler:IHandleMessages<EndpointHeartbeatReceived>
    {
        public HeartbeatMonitor HeartbeatMonitor { get; set; }

        public void Handle(EndpointHeartbeatReceived message)
        {
            HeartbeatMonitor.RegisterHeartbeat(message.Endpoint,message.Machine,message.SentAt);
        }
    }
}