namespace ServiceControl.EventLog.Definitions
{
    using Recoverability;

    class MessageFailedInStagingDefinition : EventLogMappingDefinition<MessageFailedInStaging>
    {
        public MessageFailedInStagingDefinition()
        {
            TreatAsError();
            RelatesToMessage(m => m.UniqueMessageId);
            Description(m => $"All attempts to stage message {m.UniqueMessageId} failed. The message has been removed from the retry batch. Please contact Particular Software support and provide failure details from ServiceControl log files.");
        }
    }
}