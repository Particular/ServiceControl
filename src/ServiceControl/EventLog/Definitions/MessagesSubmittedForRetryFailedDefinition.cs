namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    class MessagesSubmittedForRetryFailedDefinition : EventLogMappingDefinition<MessagesSubmittedForRetryFailed>
    {
        public MessagesSubmittedForRetryFailedDefinition()
        {
            Description(m => $"'{m.FailedMessageId}' failed to be submitted for retry to '{m.Destination}'. Reason for failure: {m.Reason}");
            RelatesToMessage(m => m.FailedMessageId);
            TreatAsError();
        }
    }
}