namespace ServiceControl.MessageFailures
{
    public class FailedMessageQueue
    {
        public string FailedMessageQueueAddress { get; set; }
        public string FailedMessageQueueDisplayName { get; set; }
        public int FailedMessageCount { get; set; }
    }
}
