namespace ServiceControl.EventLog.Definitions
{
    using System;
    using ServiceControl.Contracts.MessageFailures;

    public class FailedMessageUnArchivedDefinition : EventLogMappingDefinition<FailedMessagesUnArchived>
    {
        public FailedMessageUnArchivedDefinition()
        {
            Description(m => String.Format("{0} failed message(s) unarchived", m.MessagesCount));
        }
    }
}