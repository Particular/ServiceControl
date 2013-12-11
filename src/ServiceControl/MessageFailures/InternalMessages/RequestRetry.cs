namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class RequestRetry : ICommand
    {
        public string FailedMessageId { get; set; }
    }

    public class PerformRetry : ICommand
    {
        public string MessageId { get; set; }
        public Address TargetEndpointAddress { get; set; }
    }
}