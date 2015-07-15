namespace ServiceControl.Recoverability
{
    using ServiceControl.MessageFailures;

    public class FailureGroupMessageView
    {
        public string FailureGroupId { get; set; }
        public FailedMessageStatus Status { get; set; }
        public string MessageId { get; set; }
    }
}