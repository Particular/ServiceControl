namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class MessagesSubmittedForRetryDefinition : EventLogMappingDefinition<MessagesSubmittedForRetry>
    {
        public MessagesSubmittedForRetryDefinition()
        {
            Description(m => string.IsNullOrWhiteSpace(m.Context) 
                ? $"{m.FailedMessageIds.Length} failed message(s) submitted for retry"
                : $"{m.Context} containing {m.FailedMessageIds.Length} message(s)"
                );
            RelatesToMessages(m => m.FailedMessageIds);
        }
    }
}