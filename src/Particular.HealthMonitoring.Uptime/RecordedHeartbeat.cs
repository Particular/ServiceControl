namespace ServiceControl.Monitoring
{
    using System;

    class RecordedHeartbeat
    {
        public readonly DateTime? Timestamp;
        public readonly HeartbeatStatus Status;

        public RecordedHeartbeat(HeartbeatStatus status, DateTime? timestamp)
        {
            Status = status;
            Timestamp = timestamp;
        }
    }
}