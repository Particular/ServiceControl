namespace ServiceControl.MessageFailures.InternalMessages
{
    using Infrastructure.Auth;
    using NServiceBus;

    class ArchiveMessage : ICommand
    {
        public string FailedMessageId { get; set; }

        // Scope of the originating operation, carried so the per-message audit entry emitted when
        // the message is really archived matches the operation entry (single vs batch). Defaults to
        // Single for legacy in-flight commands.
        public MessageActionScope Scope { get; set; }
    }
}