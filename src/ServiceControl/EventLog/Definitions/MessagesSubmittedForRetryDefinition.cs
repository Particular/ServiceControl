namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class MessagesSubmittedForRetryDefinition : EventLogMappingDefinition<MessagesSubmittedForRetry>
    {
        public MessagesSubmittedForRetryDefinition()
        {
            Description(m => string.IsNullOrWhiteSpace(m.Context) 
                ? string.Format("{0} failed message(s) submitted for retry", m.FailedMessageIds.Length)
                : string.Format("{0} containing {1} message(s)", m.Context, m.FailedMessageIds.Length)
            );
            RelatesToMessages(m => m.FailedMessageIds);
        }
    }
}