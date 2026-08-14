namespace ServiceControl.Recoverability
{
    using System;
    using MessageFailures;

    public class FailureGroupMessageView : IHaveStatus
    {
        public required string Id { get; set; }
        public required string FailureGroupId { get; set; }
        public required string FailureGroupName { get; set; }
        public required string MessageId { get; set; }
        public DateTime TimeSent { get; set; }
        public required string MessageType { get; set; }
        public DateTime TimeOfFailure { get; set; }
        public long LastModified { get; set; }
        public FailedMessageStatus Status { get; set; }
    }
}