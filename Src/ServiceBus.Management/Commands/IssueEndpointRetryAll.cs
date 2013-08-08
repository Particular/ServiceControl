namespace ServiceBus.Management.Commands
{
    using NServiceBus;

    public class IssueEndpointRetryAll : ICommand
    {
        public string EndpointName { get; set; }
    }
}