namespace ServiceControl.EventLog.Definitions
{
    using System;
    using ServiceControl.InternalContracts.Messages.Recoverability;

    public class MessagesSubmittedForRetryDefinition : EventLogMappingDefinition<MessagesSubmittedForRetry>
    {
        public MessagesSubmittedForRetryDefinition()
        {
            Description(m => String.Format("{0} failed messages submitted for retry", m.FailedMessageIds.Length));

            RelatesToMessages(m => m.FailedMessageIds);
        }
    }
}