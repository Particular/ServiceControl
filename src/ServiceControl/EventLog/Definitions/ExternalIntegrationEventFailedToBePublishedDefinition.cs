namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.ExternalIntegrations;

    class ExternalIntegrationEventFailedToBePublishedDefinition : EventLogMappingDefinition<ExternalIntegrationEventFailedToBePublished>
    {
        public ExternalIntegrationEventFailedToBePublishedDefinition()
        {
            Description(m => string.Format("'{0}' failed to be published to other integration points. Reason for failure: {1}", m.EventType, m.Reason));
            TreatAsError();
        }
    }
}