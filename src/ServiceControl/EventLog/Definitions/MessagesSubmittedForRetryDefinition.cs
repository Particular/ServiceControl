namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class MessagesSubmittedForRetryDefinition : EventLogMappingDefinition<MessagesSubmittedForRetry>
    {
        public MessagesSubmittedForRetryDefinition()
        {
            Description(m => string.Format("{0} failed messages submitted for retry", m.FailedMessageIds.Length));
            RelatesToMessages(m => m.FailedMessageIds);
        }
    }
}