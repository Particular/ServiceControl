namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    class RequestRetryAll : ICommand
    {
        public string Endpoint { get; set; }
    }
}