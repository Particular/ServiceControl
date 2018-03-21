namespace Particular.HealthMonitoring.Uptime
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Particular.HealthMonitoring.Uptime.Api;
    using ServiceControl.Api;
    using ServiceControl.Infrastructure.DomainEvents;

    public class UptimeMonitoringDependencies
    {
        public IPersistEndpointUptimeInformation Persister { get; set; }
        public IDomainEvents DomainEvents { get; set; }
    }

    public class UptimeMonitoring : IComponent
    {
        EndpointInstanceMonitoring monitoring;
        private EndpointUptimeInformationPersister persister;

        public UptimeMonitoring()
        {
            monitoring = new EndpointInstanceMonitoring();
        }
        
        public async Task<ComponentOutput> Initialize(ComponentInput input)
        {
            persister = new EndpointUptimeInformationPersister(input.DocumentStore);
            var events = await persister.Load().ConfigureAwait(false);
            monitoring.Initialize(events);
            
            // The stuff here needs to be provide to SC
            var modules = new UptimeApiModule(monitoring, persister);
            var detector = new HeartbeatFailureDetector(monitoring, input.DomainEvents, persister);
            var heartbeatProcessor = new HeartbeatProcessor(monitoring, input.DomainEvents, persister);
            var auditProcessor = new EndpointDetectingProcessor(monitoring);

            return new Output(auditProcessor);
        }

        public Task TearDown()
        {
            return Task.FromResult(0);
        }
    }
}