namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class MarkPendingRetryAsResolved : ICommand
    {
        public string FailedMessageId { get; set; }
    }
}