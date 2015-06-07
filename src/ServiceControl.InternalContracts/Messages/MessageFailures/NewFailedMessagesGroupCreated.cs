namespace ServiceControl.InternalContracts.Messages.MessageFailures
{
    using NServiceBus;

    public class NewFailedMessagesGroupCreated : IEvent
    {
        public string Id { get; set; }
    }
}
