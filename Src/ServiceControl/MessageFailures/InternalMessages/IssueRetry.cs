namespace ServiceBus.Management.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class IssueRetry : ICommand
    {
        public string MessageId { get; set; }
    }
}