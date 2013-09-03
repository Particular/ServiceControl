namespace ServiceControl.HeartbeatMonitoring
{
    using NServiceBus;

    public class HeartbeatSummaryChanged : IEvent
    {
        public int ActiveEndpoints { get; set; }
        public int NumberOfFailingEndpoints { get; set; }
    }
}