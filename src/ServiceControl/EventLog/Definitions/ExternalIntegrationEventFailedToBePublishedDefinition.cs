namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.ExternalIntegrations;

    class ExternalIntegrationEventFailedToBePublishedDefinition : EventLogMappingDefinition<ExternalIntegrationEventFailedToBePublished>
    {
        public ExternalIntegrationEventFailedToBePublishedDefinition()
        {
            Description(m => $"'{m.EventType}' failed to be published to other integration points. Reason for failure: {m.Reason}");
            TreatAsError();
        }
    }
}