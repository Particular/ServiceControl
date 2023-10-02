namespace ServiceControl.SagaAudit
{
    using System;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EndpointReportingSagaAuditToPrimary : IDomainEvent
    {
        public string EndpointName { get; set; }
        public DateTime DetectedAt { get; set; }
    }
}