namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageRedirects;

    public class MessageRedirectRemovedDefinition : EventLogMappingDefinition<MessageRedirectRemoved>
    {
        public MessageRedirectRemovedDefinition()
        {
            Description(m => $"Redirect from '{m.FromPhysicalAddress}' to '{m.ToPhysicalAddress}' was removed.");
        }
    }
}