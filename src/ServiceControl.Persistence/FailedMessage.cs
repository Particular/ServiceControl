namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;

    public class FailedMessage : IHaveStatus
    {
        public FailedMessage()
        {
            // these ID fields *should* be marked as required
            // but there seem to be some wire usages of this type for 
            // deserialisation that omit the UniqueMessageId on output
            Id = string.Empty;
            UniqueMessageId = string.Empty;
            ProcessingAttempts = [];
            FailureGroups = [];
        }

        public required string Id { get; set; }
        public string UniqueMessageId { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public List<FailureGroup> FailureGroups { get; set; }


        public FailedMessageStatus Status { get; set; }


        public class ProcessingAttempt
        {
            public ProcessingAttempt()
            {
                MessageMetadata = [];
                Headers = [];
            }

            public Dictionary<string, object> MessageMetadata { get; set; }
            public FailureDetails FailureDetails { get; set; } = new();
            public DateTime AttemptedAt { get; set; }
            public string? MessageId { get; set; }
            public string? Body { get; set; }
            public Dictionary<string, string> Headers { get; set; }
        }

        public class FailureGroup
        {
            public required string Id { get; set; }
            public string? Title { get; set; }
            public string? Type { get; set; }
        }
    }

    public class GroupComment
    {
        public required string Id { get; set; }
        public string? Comment { get; set; }
    }

    public enum FailedMessageStatus
    {
        Unresolved = 1,
        Resolved = 2,
        RetryIssued = 3,
        Archived = 4
    }
}