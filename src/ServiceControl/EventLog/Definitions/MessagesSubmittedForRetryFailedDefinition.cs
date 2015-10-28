namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    class MessagesSubmittedForRetryFailedDefinition : EventLogMappingDefinition<MessagesSubmittedForRetryFailed>
    {
        public MessagesSubmittedForRetryFailedDefinition()
        {
            Description(m => string.Format("'{0}' failed to be submitted for retry to '{1}'. Reason for failure: {2}", m.FailedMessageId, m.Destination, m.Reason));
            RelatesToMessage(m => m.FailedMessageId);
            TreatAsError();
        }
    }
}