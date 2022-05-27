namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageRedirects;
    using ServiceControl.EventLog;

    class MessageRedirectRemovedDefinition : EventLogMappingDefinition<MessageRedirectRemoved>
    {
        public MessageRedirectRemovedDefinition()
        {
            Description(m => $"Redirect from '{m.FromPhysicalAddress}' to '{m.ToPhysicalAddress}' was removed.");
        }
    }
}