namespace ServiceControl.Recoverability
{
    using ServiceControl.MessageFailures;

    public class FailureGroupMessageView: IHaveStatus
    {
        public string Id { get; set; }
        public string FailureGroupId { get; set; }
        public string FailureGroupName { get; set; }
        public FailedMessageStatus Status { get; set; }
        public string MessageId { get; set; }
    }
}