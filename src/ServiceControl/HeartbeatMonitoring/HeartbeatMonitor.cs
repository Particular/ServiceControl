namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;

    public class HeartbeatMonitor
    {
        public HeartbeatMonitor(IBus bus)
        {
            this.bus = bus;
            GracePeriod = TimeSpan.FromSeconds(60);
        }

        public TimeSpan GracePeriod { get; set; }

        public List<HeartbeatStatus> HeartbeatStatuses
        {
            get { return endpointInstancesBeingMonitored.Values.ToList(); }
        }

        public void Start()
        {
            timer = new Timer(RefreshHeartbeatsStatuses, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public void Stop()
        {
            timer.Dispose();
        }

        public void RegisterHeartbeat(string endpoint, string machine, DateTime sentAt)
        {
            var endpointInstanceId = endpoint + machine;

            endpointInstancesBeingMonitored.AddOrUpdate(endpointInstanceId, 
                s =>
                {
                    bus.Publish(new EndpointDetected
                    {
                        Endpoint = endpoint,
                        Machine = machine,
                        At = sentAt,
                    });

                    return new HeartbeatStatus
                    {
                        Endpoint = endpoint,
                        Machine = machine,
                        LastSentAt = sentAt,
                        Active = true
                    };
                },
                (e, status) =>
                {
                    if (status.LastSentAt < sentAt)
                    {
                        status.LastSentAt = sentAt;
                    }

                    return status;
                });
        }

        public void RefreshHeartbeatsStatuses(object state)
        {
            foreach (var status in endpointInstancesBeingMonitored.Values)
            {
                var newStatus = IsActive(status);

                if (status.Active == newStatus)
                {
                    continue;
                }

                status.Active = newStatus;

                if (status.Active)
                {
                    bus.Publish(new EndpointRestored
                    {
                        Endpoint = status.Endpoint,
                        Machine = status.Machine,
                        At = status.LastSentAt,
                    });
                }
                else
                {
                    bus.Publish(new EndpointFailedToHeartbeat
                    {
                        Endpoint = status.Endpoint,
                        Machine = status.Machine,
                        LastReceivedAt = status.LastSentAt,
                    });
                }
            }
        }

        bool IsActive(HeartbeatStatus status)
        {
            var timeSinceLastHeartbeat = DateTime.UtcNow - status.LastSentAt;

            return timeSinceLastHeartbeat < GracePeriod;
        }

        readonly IBus bus;

        readonly ConcurrentDictionary<string, HeartbeatStatus> endpointInstancesBeingMonitored =
            new ConcurrentDictionary<string, HeartbeatStatus>();

        Timer timer;
    }
}