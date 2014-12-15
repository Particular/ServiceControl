namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;

    public class ImportSuccessfullyProcessedMessage : ImportMessage
    {
        public ImportSuccessfullyProcessedMessage(TransportMessage message, string uniqueId) : base(message, uniqueId)
        {
        }
    }
}
