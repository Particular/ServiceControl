namespace ServiceControl.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using Raven.Client;

    public class RaiseCustomCheckChanges : IWantToRunWhenBusStartsAndStops
    {
        public RaiseCustomCheckChanges(IBus bus, IDocumentStore store)
        {
            this.bus = bus;
            this.store = store;
        }

        public void Start()
        {
            timer = new Timer(Run, null, 0, -1);
        }

        public void Stop()
        {
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                timer.Dispose(manualResetEvent);

                manualResetEvent.WaitOne();
            }
        }

        void Run(object _)
        {
            using (var session = store.OpenSession())
            {
                var newTotal = session.Query<CustomCheck>().Count(c => c.Status == Status.Fail);

                if (newTotal == total)
                {
                    return;
                }

                total = newTotal;
            }

            bus.Publish(new TotalCustomCheckUpdated {Total = total});

            try
            {
                timer.Change((int)TimeSpan.FromSeconds(10).TotalMilliseconds, -1);
            }
            catch (ObjectDisposedException)
            { }
        }

        readonly IBus bus;
        readonly IDocumentStore store;
        Timer timer;
        int total;
    }
}