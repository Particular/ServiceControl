namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using System.Threading.Tasks;
    using Particular.HealthMonitoring.Uptime.Api;
    using Particular.Operations.Audits.Api;
    using Particular.Operations.Errors.Api;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    class EndpointDetectingProcessor : IProcessAudits, IProcessErrors
    {
        EndpointInstanceMonitoring monitoring;
        IDomainEvents domainEvents;
        IPersistEndpointUptimeInformation persister;

        public EndpointDetectingProcessor(EndpointInstanceMonitoring monitoring, IDomainEvents domainEvents, IPersistEndpointUptimeInformation persister)
        {
            this.monitoring = monitoring;
            this.domainEvents = domainEvents;
            this.persister = persister;
        }
        
        public async Task Handle(AuditMessage message)
        {
            await TryAddEndpoint(message.ProcessingEndpoint).ConfigureAwait(false);
            await TryAddEndpoint(message.SendingEndpoint).ConfigureAwait(false);
        }

        public async Task Handle(ErrorMessage message)
        {
            await TryAddEndpoint(message.ProcessingEndpoint).ConfigureAwait(false);
            await TryAddEndpoint(message.SendingEndpoint).ConfigureAwait(false);
        }

        Task TryAddEndpoint(EndpointDetails endpointDetails)
        {
            if (endpointDetails == null)
            {
                return Task.FromResult(0);
            }

            // for backwards compat with version before 4_5 we might not have a hostid
            if (endpointDetails.HostId == Guid.Empty)
            {
                return Task.FromResult(0);
            }

            // TODO: What to do with the heartbeat event?
            var @event = monitoring.StartTrackingEndpoint(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);
            domainEvents.Raise(@event);
            return persister.Store(new [] { @event });
        }
    }
}