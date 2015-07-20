namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class MessagesSubmittedForRetryDefinition : EventLogMappingDefinition<MessagesSubmittedForRetry>
    {
        public MessagesSubmittedForRetryDefinition()
        {
            Description(m => string.IsNullOrWhiteSpace(m.Context) 
                ? string.Format("{0} failed messages submitted for retry", m.FailedMessageIds.Length)
                : string.Format("{0} failed messages submitted for retry: {1}", m.FailedMessageIds.Length, m.Context)
            );
            RelatesToMessages(m => m.FailedMessageIds);
        }
    }
}