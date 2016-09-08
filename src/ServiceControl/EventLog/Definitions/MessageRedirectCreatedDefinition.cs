namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Contracts.MessageRedirects;

    public class MessageRedirectCreatedDefinition : EventLogMappingDefinition<MessageRedirectCreated>
    {
        public MessageRedirectCreatedDefinition()
        {
            Description(m => $"New redirect from '{m.FromPhysicalAddress}' to '{m.ToPhysicalAddress}' has been created.");
        }
    }
}