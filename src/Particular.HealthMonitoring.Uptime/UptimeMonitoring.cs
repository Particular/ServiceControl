namespace Particular.HealthMonitoring.Uptime
{
    using System.Threading.Tasks;
    using ServiceControl.Api;

    public class UptimeMonitoring : IComponent
    {
        public async Task<ComponentOutput> Initialize(ComponentInput input)
        {
            var monitoring = new EndpointInstanceMonitoring();

            var persister = new EndpointUptimeInformationPersister(input.DocumentStore);
            var events = await persister.Load().ConfigureAwait(false);
            monitoring.Initialize(events);
            
            var apiModule = new UptimeApiModule(monitoring, persister);
            var failureDetector = new HeartbeatFailureDetector(monitoring, input.DomainEvents, persister);
            var heartbeatProcessor = new HeartbeatProcessor(monitoring, input.DomainEvents, persister);
            var endpointDetector = new EndpointDetectingProcessor(monitoring);

            return new Output(endpointDetector, endpointDetector, heartbeatProcessor, apiModule, failureDetector);
        }

        public Task TearDown()
        {
            return Task.FromResult(0);
        }
    }
}