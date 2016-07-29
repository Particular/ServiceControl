namespace ServiceControl.EventLog.Definitions
{
    using ServiceControl.Contracts.MessageRedirects;

    public class MessageRedirectChangedDefinition : EventLogMappingDefinition<MessageRedirectChanged>
    {
        public MessageRedirectChangedDefinition()
        {
            Description(m => $"Redirect from '{m.FromPhysicalAddress}' to '{m.PreviousToPhysicalAddress}' has been updated to '{m.ToPhysicalAddress}'.");
        }
    }
}