namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using System.Threading;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using Raven.Client;

    public class RaiseHartbeatChanges : IWantToRunWhenBusStartsAndStops
    {
        public IDocumentStore Store { get; set; }

        public RaiseHartbeatChanges(IBus bus)
        {
            this.bus = bus;
        }

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
            using (var session = Store.OpenSession())
            {
                var numberOfEndpointsDead = session.Query<Heartbeat>().Count(c => c.ReportedStatus == Status.Dead);
                var numberOfEndpointsActive = session.Query<Heartbeat>().Count(c => c.ReportedStatus != Status.Dead);

                bus.Publish(new TotalEndpointsUpdated
                {
                    Active = numberOfEndpointsActive,
                    Failing = numberOfEndpointsDead
                });
            }

            try
            {
                //timer.Change((int)TimeSpan.FromSeconds(5).TotalMilliseconds, -1);
            }
            catch (ObjectDisposedException)
            { }
        }

        Timer timer;
        readonly IBus bus;
    }
}