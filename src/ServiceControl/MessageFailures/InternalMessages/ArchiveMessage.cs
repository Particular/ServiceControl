namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class ArchiveMessage : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}