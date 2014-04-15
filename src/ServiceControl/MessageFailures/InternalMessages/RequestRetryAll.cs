namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class RequestRetryAll : ICommand
    {
        public string Endpoint { get; set; }
    }
}