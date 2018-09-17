namespace ServiceControl.EventLog.Definitions
{
    using Contracts.MessageRedirects;

    class MessageRedirectChangedDefinition : EventLogMappingDefinition<MessageRedirectChanged>
    {
        public MessageRedirectChangedDefinition()
        {
            Description(m => $"Redirect from '{m.FromPhysicalAddress}' to '{m.PreviousToPhysicalAddress}' has been updated to '{m.ToPhysicalAddress}'.");
        }
    }
}