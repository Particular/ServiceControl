namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class RetryMessagesById : ICommand
    {
        public string[] MessageUniqueIds { get; set; }
    }
}
