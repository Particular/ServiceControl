namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class RetryPendingMessagesById : ICommand
    {
        public string[] MessageUniqueIds { get; set; }
    }
}