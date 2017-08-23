namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;

    class HeartbeatMonitor
    {
        volatile RecordedHeartbeat heartbeat = new RecordedHeartbeat(HeartbeatStatus.Unknown, null);

        public void MarkAlive(DateTime timestamp)
        {
            var newReading = new RecordedHeartbeat(HeartbeatStatus.Alive, timestamp);
            var done = false;

            while (!done)
            {
                var priorReading = heartbeat;

                if (priorReading.Timestamp.GetValueOrDefault() <= timestamp)
                {
                    done = Interlocked.CompareExchange(ref heartbeat, newReading, priorReading) == priorReading;
                }
                else
                {
                    done = true;
                }
            }
        }

        public RecordedHeartbeat MarkDeadIfOlderThan(DateTime cutoff)
        {
            var done = false;

            while (!done)
            {
                var priorReading = heartbeat;
                if (priorReading.Timestamp.GetValueOrDefault() < cutoff)
                {
                    var newReading = new RecordedHeartbeat(HeartbeatStatus.Dead, priorReading.Timestamp);
                    done = Interlocked.CompareExchange(ref heartbeat, newReading, priorReading) == priorReading;
                }
                else
                {
                    done = true;
                }
            }

            return heartbeat;
        }

        public bool IsSendingHeartbeats() => heartbeat?.Status == HeartbeatStatus.Alive;
    }
}