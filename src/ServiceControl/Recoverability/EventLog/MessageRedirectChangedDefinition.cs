namespace ServiceControl.Recoverability.EventLog
{
    using Contracts.MessageRedirects;
    using ServiceControl.EventLog;

    class MessageRedirectChangedDefinition : EventLogMappingDefinition<MessageRedirectChanged>
    {
        public MessageRedirectChangedDefinition()
        {
            Description(m => $"Redirect from '{m.FromPhysicalAddress}' to '{m.PreviousToPhysicalAddress}' has been updated to '{m.ToPhysicalAddress}'.");
        }
    }
}