namespace ServiceControl.MessageFailures
{
    public class QueueAddress
    {
        public string PhysicalAddress { get; set; }
        public int FailedMessageCount { get; set; }
    }
}
