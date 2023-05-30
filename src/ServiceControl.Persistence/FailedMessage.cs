﻿namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Contracts.Operations;

    public class FailedMessage : IHaveStatus
    {
        public FailedMessage()
        {
            ProcessingAttempts = new List<ProcessingAttempt>();
            FailureGroups = new List<FailureGroup>();
        }

        public string Id { get; set; }

        public List<ProcessingAttempt> ProcessingAttempts { get; set; }
        public List<FailureGroup> FailureGroups { get; set; }

        public string UniqueMessageId { get; set; }

        public FailedMessageStatus Status { get; set; }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return $"{CollectionName}/{messageUniqueId}";
        }

        internal static string GetMessageIdFromDocumentId(string failedMessageDocumentId)
        {
            return failedMessageDocumentId.Substring(CollectionName.Length + 1);
        }

        public const string CollectionName = "FailedMessages";

        public class ProcessingAttempt
        {
            public ProcessingAttempt()
            {
                MessageMetadata = new Dictionary<string, object>();
                Headers = new Dictionary<string, string>();
            }

            public Dictionary<string, object> MessageMetadata { get; set; }
            public FailureDetails FailureDetails { get; set; }
            public DateTime AttemptedAt { get; set; }
            public string MessageId { get; set; }
            public string Body { get; set; }
            public Dictionary<string, string> Headers { get; set; }
        }

        public class FailureGroup
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Type { get; set; }
        }
    }

    public class GroupComment
    {
        public string Id { get; set; }
        public string Comment { get; set; }

        public static string MakeId(string groupId)
        {
            return $"GroupComment/{groupId}";
        }
    }

    public enum FailedMessageStatus
    {
        Unresolved = 1,
        Resolved = 2,
        RetryIssued = 3,
        Archived = 4
    }
}