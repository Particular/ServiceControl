namespace ServiceControl.SagaAudit
{
    using ServiceControl.EventLog;

    class EndpointReportingSagaAuditToPrimaryDefinition : EventLogMappingDefinition<EndpointReportingSagaAuditToPrimary>
    {
        public EndpointReportingSagaAuditToPrimaryDefinition()
        {
            Severity(EventLog.Severity.Warning);

            Description(m => $"Endpoint {m.EndpointName} is configured to send saga audit data to the primary ServiceControl queue. Instead, saga audit data should be sent to the Audit Queue Name configured in the ServiceControl Audit Instance.");

            RelatesToEndpoint(m => m.EndpointName);

            RaisedAt(m => m.DetectedAt);
        }
    }
}