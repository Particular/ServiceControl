namespace ServiceControl.MessageFailures.Api
{
    public class FailedMessageQueueView
    {
        public string FailedQueueAddress { get; set; }
        public int FailedMessageCount { get; set; }
    }
}