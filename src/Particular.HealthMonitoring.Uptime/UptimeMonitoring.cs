namespace Particular.HealthMonitoring.Uptime
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Particular.HealthMonitoring.Uptime.Api;
    using Particular.Operations.Audits.Api;
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
            return new object[]
            {
                new UptimeApiModule(monitoring),
                new HeartbeatFailureDetector(monitoring, dependencies.DomainEvents, dependencies.Persister),
                new HeartbeatProcessor(monitoring, dependencies.DomainEvents, dependencies.Persister),
                new EndpointDetectingProcessor(monitoring)
            };
        }

        public Task TearDown()
        {
            return Task.FromResult(0);
        }
    }

    class Output : ComponentOutput, IProvideAuditProcessor
    {
        public Output(IProcessAudits auditProcessor)
        {
            ProcessAudits = auditProcessor;
        }
        public IProcessAudits ProcessAudits { get; }
    }
}