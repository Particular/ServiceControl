namespace ServiceControl.Monitoring.Infrastructure
{
    using System.Threading.Tasks;
    using NServiceBus;

    public class BusInstance
    {
        public BusInstance(IEndpointInstance bus)
        {
            this.bus = bus;
        }

        public Task Stop()
        {
            return bus.Stop();
        }

        IEndpointInstance bus;
    }
}