namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;

    public class HeartbeatMonitor:INeedInitialization
    {
        public HeartbeatMonitor()
        {
            GracePeriod = TimeSpan.FromSeconds(60);
        }

        public void RegisterHeartbeat(string endpoint, string machine, DateTime sentAt)
        {
            var endpointInstanceId = endpoint + machine;

            endpointInstancesBeingMonitored.AddOrUpdate(endpointInstanceId, e => new HeartbeatStatus { Endpoint = endpoint, Machine = machine, LastHeartbeatSentAt = sentAt },
                (e, status) =>
                {
                    if (status.LastHeartbeatSentAt < sentAt)
                        status.LastHeartbeatSentAt = sentAt;

                    return status;
                });
        }

        public void CheckForMissingHeartbeats()
        {
            foreach (var status in endpointInstancesBeingMonitored.Values)
            {
                var timeSinceLastHeartbeat = DateTime.UtcNow - status.LastHeartbeatSentAt;

                if (timeSinceLastHeartbeat >= GracePeriod)
                {
                    status.Failing = true;
                }
                else
                {
                    status.Failing = false;
                }
            }
        }

        public TimeSpan GracePeriod { get; set; }

        public IEnumerable<HeartbeatStatus> CurrentStatus()
        {
            return endpointInstancesBeingMonitored.Values.ToList();
        }

        public void Init()
        {
            Configure.Component<HeartbeatMonitor>(DependencyLifecycle.SingleInstance);
        }

        readonly ConcurrentDictionary<string, HeartbeatStatus> endpointInstancesBeingMonitored =
          new ConcurrentDictionary<string, HeartbeatStatus>();



        public class HeartbeatStatus
        {
            public bool Failing { get; set; }

            public string Endpoint { get; set; }

            public string Machine { get; set; }

            public DateTime LastHeartbeatSentAt { get; set; }
        }

     
    }
}