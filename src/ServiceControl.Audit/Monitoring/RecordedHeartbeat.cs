namespace ServiceControl.Monitoring
{
    using System;

    class RecordedHeartbeat
    {
        public RecordedHeartbeat(HeartbeatStatus status, DateTime? timestamp)
        {
            Status = status;
            Timestamp = timestamp;
        }

        public readonly DateTime? Timestamp;
        public readonly HeartbeatStatus Status;
    }
}