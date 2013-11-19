namespace ServiceControl.HeartbeatMonitoring
{
    using NServiceBus;

    class TotalEndpointsUpdated : IEvent
    {
        public int Active { get; set; }
        public int Failing { get; set; }
    }
}