namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class RetryPendingMessagesById : ICommand
    {
        public string[] MessageUniqueIds { get; set; }
    }
}