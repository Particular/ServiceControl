namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageFailures;

    public class MessageFailureResolvedManuallyDefinition : EventLogMappingDefinition<MessageFailureResolvedManually>
    {
        public MessageFailureResolvedManuallyDefinition()
        {
            Description(_ => "Failed message resolved manually by user");
            RelatesToMessage(m => m.FailedMessageId);
        }
    }
}