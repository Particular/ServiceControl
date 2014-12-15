namespace ServiceControl.Contracts.HeartbeatMonitoring
{
    using System;
    using NServiceBus;

    public class HeartbeatsUpdated : IEvent
    {
        public HeartbeatsUpdated()
        {
            RaisedAt = DateTime.UtcNow;
        }

        public int Active { get; set; }
        public int Failing { get; set; }
        public DateTime RaisedAt { get; set; }
    }
}