namespace ServiceControl.Recoverability
{
    using System;
    using ServiceControl.MessageFailures;

    public class FailureGroupMessageView: IHaveStatus
    {
        public string Id { get; set; }
        public string FailureGroupId { get; set; }
        public string FailureGroupName { get; set; }
        public FailedMessageStatus Status { get; set; }
        public string MessageId { get; set; }
        public DateTime TimeSent { get; set; }
        public string MessageType { get; set; }
    }
}