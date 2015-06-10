namespace ServiceControl.Groups.Indexes
{
    using System;
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class MessageFailuresByFailureGroupsIndex : AbstractIndexCreationTask<MessageFailureHistory>
    {
        public class StoredFields
        {
            public string FailedMessageId { get; set; }
            public string FailureGroups_Id { get; set; }
            public DateTime LastAttempt { get; set; }
        }

        public MessageFailuresByFailureGroupsIndex()
        {
            Map = failures => from failure in failures
                              where failure.Status == FailedMessageStatus.Unresolved
                              let lastAttemt = failure.ProcessingAttempts.OrderByDescending(a => a.AttemptedAt).Last()
                from failureGroup in failure.FailureGroups
                select new
                {
                    FailedMessageId = failure.UniqueMessageId,
                    FailureGroups_Id = failureGroup.Id, 
                    LastAttempt = lastAttemt.AttemptedAt
                };

            Store("FailedMessageId", FieldStorage.Yes);
            Store("FailureGroups_Id", FieldStorage.Yes);
            Store("LastAttempt", FieldStorage.Yes);
        }
    }
}