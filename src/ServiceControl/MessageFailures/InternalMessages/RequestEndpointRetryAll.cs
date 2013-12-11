namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class RequestEndpointRetryAll : ICommand
    {
        public string EndpointName { get; set; }
    }
}