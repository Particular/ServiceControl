﻿namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Recoverability;

    public class FailedMessageGroupArchivedDefinition : EventLogMappingDefinition<FailedMessageGroupArchived>
    {
        public FailedMessageGroupArchivedDefinition()
        {
            Description(m => $"Archived {m.MessagesCount} messages from group: {m.GroupName}");
            RelatesToGroup(m => m.GroupId);
        }
    }
}
