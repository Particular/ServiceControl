namespace ServiceBus.Management.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class IssueEndpointRetryAll : ICommand
    {
        public string EndpointName { get; set; }
    }
}