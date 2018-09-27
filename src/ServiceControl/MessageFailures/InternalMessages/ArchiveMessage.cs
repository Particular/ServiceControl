namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class ArchiveMessage : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}