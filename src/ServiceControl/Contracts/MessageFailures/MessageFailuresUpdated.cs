namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class MessageFailuresUpdated: IEvent
    {
        public int Total { get; set; }
    }
}