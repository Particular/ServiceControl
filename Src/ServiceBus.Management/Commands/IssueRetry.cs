namespace ServiceBus.Management.Commands
{
    using NServiceBus;

    public class IssueRetry : ICommand
    {
        public string MessageId { get; set; }
    }
}