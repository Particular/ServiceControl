namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using Configure = NServiceBus.Configure;

    public class RegisterHeartbeatMonitor : INeedInitialization
    {
        public void Init()
        {
            Configure.Component<HeartbeatMonitor>(DependencyLifecycle.SingleInstance);
        }
    }

    public class HeartbeatMonitorStarter : IWantToRunWhenBusStartsAndStops
    {
        readonly HeartbeatMonitor monitor;

        public HeartbeatMonitorStarter(HeartbeatMonitor monitor)
        {
            this.monitor = monitor;
        }

        public void Start()
        {
            monitor.Start();
        }

        public void Stop()
        {
            monitor.Stop();
        }
    }

    public class HeartbeatMonitor
    {
        public HeartbeatMonitor()
        {
            GracePeriod = TimeSpan.FromSeconds(60);
        }

        public TimeSpan GracePeriod { get; set; }

        public IBus Bus { get; set; }

        public void Start()
        {
            timer = new Timer(PerformCheck, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public void Stop()
        {
            timer.Dispose();
        }

        void PerformCheck(object state)
        {
            if (RefreshHeartbeatsStatuses())
            {
                var endpointStatus = CurrentStatus();

                Bus.Publish(new HeartbeatSummaryChanged
                {
                    ActiveEndpoints = endpointStatus.Count(s => s.Failing.HasValue && !s.Failing.Value),
                    NumberOfFailingEndpoints = endpointStatus.Count(s => s.Failing.HasValue && s.Failing.Value)
                });
            }
        }

        public void RegisterHeartbeat(string endpoint, string machine, DateTime sentAt)
        {
            var endpointInstanceId = endpoint + machine;

            endpointInstancesBeingMonitored.AddOrUpdate(endpointInstanceId,
                new HeartbeatStatus {Endpoint = endpoint, Machine = machine, LastHeartbeatSentAt = sentAt},
                (e, status) =>
                {
                    if (status.LastHeartbeatSentAt < sentAt)
                    {
                        status.LastHeartbeatSentAt = sentAt;
                    }

                    return status;
                });
        }

        public bool RefreshHeartbeatsStatuses()
        {
            var modified = false;

            foreach (var status in endpointInstancesBeingMonitored.Values)
            {
                var newStatus = IsFailing(status);
                if (status.Failing != newStatus)
                {
                    status.Failing = newStatus;
                    modified = true;
                }
            }

            return modified;
        }

        public List<HeartbeatStatus> CurrentStatus()
        {
            return endpointInstancesBeingMonitored.Values.ToList();
        }

        bool IsFailing(HeartbeatStatus status)
        {
            var timeSinceLastHeartbeat = DateTime.UtcNow - status.LastHeartbeatSentAt;

            return timeSinceLastHeartbeat >= GracePeriod;
        }

        readonly ConcurrentDictionary<string, HeartbeatStatus> endpointInstancesBeingMonitored =
            new ConcurrentDictionary<string, HeartbeatStatus>();

        Timer timer;
    }
}