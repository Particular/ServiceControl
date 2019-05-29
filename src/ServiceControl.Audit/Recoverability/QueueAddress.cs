namespace ServiceControl.Audit.Recoverability
{
    public class QueueAddress
    {
        public string PhysicalAddress { get; set; }
        public int FailedMessageCount { get; set; }
    }
}