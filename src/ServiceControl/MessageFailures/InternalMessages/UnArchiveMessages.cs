namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class UnArchiveMessages : ICommand
    {
        public string[] FailedMessageIds { get; set; }
    }
}