namespace ServiceControl.EventLog.Definitions
{
    using Recoverability;

    class MessagesSubmittedForRetryDefinition : EventLogMappingDefinition<MessagesSubmittedForRetry>
    {
        public MessagesSubmittedForRetryDefinition()
        {
            Description(m => string.IsNullOrWhiteSpace(m.Context)
                ? $"{m.NumberOfFailedMessages} failed message(s) submitted for retry."
                : $"{m.Context} containing {m.NumberOfFailedMessages} message(s) submitted for retry."
            );
            RelatesToMessages(m => m.FailedMessageIds);
        }
    }
}