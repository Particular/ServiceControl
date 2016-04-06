namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;
    public class FailedMessagesUnArchived: IEvent
    {
        public int MessagesCount { get; set; }
    }
}