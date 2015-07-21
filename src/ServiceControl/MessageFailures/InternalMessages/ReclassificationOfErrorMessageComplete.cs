namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;

    public class ReclassificationOfErrorMessageComplete : IEvent
    {
        public int NumberofMessageReclassified { get; set; }
    }
}