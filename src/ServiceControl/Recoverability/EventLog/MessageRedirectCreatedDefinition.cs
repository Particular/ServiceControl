namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageRedirects;
    using ServiceControl.EventLog;

    class MessageRedirectCreatedDefinition : EventLogMappingDefinition<MessageRedirectCreated>
    {
        public MessageRedirectCreatedDefinition()
        {
            Description(m => $"New redirect from '{m.FromPhysicalAddress}' to '{m.ToPhysicalAddress}' has been created.");
        }
    }
}