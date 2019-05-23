namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class MarkPendingRetryAsResolved : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}