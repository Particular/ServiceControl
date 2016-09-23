namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;

    public class FailedMessagesImported : IEvent
    {
        public string[] RepeatedFailureIds { get; set; }
        public string[] NewFailureIds { get; set; }
    }
}