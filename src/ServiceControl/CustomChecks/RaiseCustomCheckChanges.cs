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
            timer = new Timer(Run, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public void Stop()
        {
            timer.Dispose();
        }

        void Run(object stateInfo)
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
        }

        readonly IBus bus;
        readonly IDocumentStore store;
        Timer timer;
        int total;
    }
}