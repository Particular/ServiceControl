namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using System.Threading.Tasks;
    using Particular.Operations.Audits.Api;
    using ServiceControl.Contracts.Operations;

    class EndpointDetectingAuditProcessor : IProcessAudits
    {
        EndpointInstanceMonitoring monitoring;

        public EndpointDetectingAuditProcessor(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
        }
        
        public async Task Handle(AuditMessage message)
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

            return monitoring.EndpointDetected(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);
        }
    }
}