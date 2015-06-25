namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;
    using NServiceBus.IdGeneration;

    public class RequestRetryAll : ICommand
    {
        public string BatchId { get; set; }
        public string Endpoint { get; set; }

        public RequestRetryAll()
        {
            BatchId = CombGuid.Generate().ToString();
        }
    }
}