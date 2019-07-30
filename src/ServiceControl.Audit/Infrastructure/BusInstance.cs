namespace ServiceControl.Audit.Infrastructure
{
    using System.Threading.Tasks;
    using Auditing;
    using NServiceBus;

    class BusInstance
    {
        public BusInstance(IEndpointInstance bus, ImportFailedAudits importFailedAudits)
        {
            ImportFailedAudits = importFailedAudits;
            this.bus = bus;
        }

        public ImportFailedAudits ImportFailedAudits { get; }

        public Task Stop()
        {
            return bus.Stop();
        }

        IEndpointInstance bus;
    }
}