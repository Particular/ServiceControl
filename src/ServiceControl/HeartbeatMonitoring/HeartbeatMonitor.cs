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
                var results = session.Query<Heartbeat, HeartbeatsIndex>()
                    .Statistics(out stats)
                  .ToArray();

                // TODO: this only goes through one page of the results. Do we need to use the stream API here instead?
                foreach (var result in results)
                {
                    if (result.ReportedStatus == Status.Beating && !IsActive(result.LastReportAt))
                    {
                        result.ReportedStatus = Status.Dead;
                        bus.Publish(new EndpointFailedToHeartbeat
                        {
                            Endpoint = result.OriginatingEndpoint.Name,
                            Machine = result.OriginatingEndpoint.Machine,
                            LastReceivedAt = result.LastReportAt,
                        });
                        session.SaveChanges();
                    }
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