namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using System.Threading;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using Raven.Client;

    public class HeartbeatMonitor : IWantToRunWhenBusStartsAndStops
    {
        public HeartbeatMonitor(IBus bus)
        {
            this.bus = bus;
            GracePeriod = TimeSpan.FromSeconds(40);
        }

        public IDocumentStore Store { get; set; }

        public TimeSpan GracePeriod { get; set; }

        public void Start()
        {
            timer = new Timer(Refresh, null, 0, -1);
        }

        public void Stop()
        {
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                timer.Dispose(manualResetEvent);

                manualResetEvent.WaitOne();
            }
        }

        void Refresh(object _)
        {
            UpdateStatuses();

            try
            {
                timer.Change((int) GracePeriod.TotalMilliseconds, -1);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        void UpdateStatuses()
        {
            using (var session = Store.OpenSession())
            {
                RavenQueryStatistics stats;
                var results = session.Query<Heartbeat>()
                    .Statistics(out stats)
                    .ToArray();

                foreach (var result in results)
                {
                    var newStatus = IsActive(result.LastReportAt) ? Status.Beating : Status.Dead;

                    if (result.ReportedStatus == newStatus)
                    {
                        continue;
                    }

                    if (result.ReportedStatus == Status.New) // New endpoint heartbeat
                    {
                        bus.Publish(new HeartbeatingEndpointDetected
                        {
                            Endpoint = result.OriginatingEndpoint.Name,
                            Machine = result.OriginatingEndpoint.Machine,
                            DetectedAt = result.LastReportAt,
                        });
                    }
                    else if (newStatus == Status.Beating)
                    {
                        bus.Publish(new EndpointHeartbeatRestored
                        {
                            Endpoint = result.OriginatingEndpoint.Name,
                            Machine = result.OriginatingEndpoint.Machine,
                            RestoredAt = result.LastReportAt
                        });
                    }
                    else
                    {
                        bus.Publish(new EndpointFailedToHeartbeat
                        {
                            Endpoint = result.OriginatingEndpoint.Name,
                            Machine = result.OriginatingEndpoint.Machine,
                            LastReceivedAt = result.LastReportAt,
                        });
                    }

                    result.ReportedStatus = newStatus;
                    session.SaveChanges();
                }
            }
        }

        bool IsActive(DateTime lastReportedAt)
        {
            var timeSinceLastHeartbeat = DateTime.UtcNow - lastReportedAt;

            return timeSinceLastHeartbeat < GracePeriod;
        }

        readonly IBus bus;

        Timer timer;
    }
}