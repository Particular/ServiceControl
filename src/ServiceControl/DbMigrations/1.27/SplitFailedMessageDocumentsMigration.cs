﻿namespace Particular.ServiceControl.DbMigrations
{
    using System.Collections.Generic;
    using System.Linq;
    using global::ServiceControl;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.MessageFailures;
    using global::ServiceControl.Recoverability;
    using Raven.Client;
    using Raven.Client.Document;

    public class SplitFailedMessageDocumentsMigration : IMigration
    {
        private readonly IFailureClassifier[] classifiers;

        public SplitFailedMessageDocumentsMigration(IFailureClassifier[] classifiers)
        {
            this.classifiers = classifiers;
        }

        public string Apply(IDocumentStore store)
        {
#pragma warning disable 618
            store.Conventions.DefaultQueryingConsistency = ConsistencyOptions.AlwaysWaitForNonStaleResultsAsOfLastWrite;
#pragma warning restore 618

            var stats = new MigrationStats();

            int retrievedResults;
            var currentPage = 0;
            List<string> toBeDeleted = new List<string>();

            do
            {
                using (var session = store.OpenSession())
                {
                    var failedMessages = session.Advanced.LoadStartingWith<FailedMessage>(
                        $"FailedMessages/",
                        start: PageSize * currentPage++,
                        pageSize: PageSize);

                    foreach (var failedMessage in failedMessages)
                    {
                        stats += MigrateFromTemporaryCollection(failedMessage, session, toBeDeleted);
                    }

                    session.SaveChanges();

                    retrievedResults = failedMessages.Length;
                }
            } while (retrievedResults > 0);

            foreach (var key in toBeDeleted)
            {
                store.DatabaseCommands.Delete(key, null);
                stats.Deleted++;
            }

            return $"Found {stats.FoundProblem} issue(s) in {stats.Checked} Failed Message document(s). Created {stats.Created} new document(s). Deleted {stats.Deleted} old document(s).";
        }

        private MigrationStats MigrateFromTemporaryCollection(FailedMessage originalFailedMessage, IDocumentSession session, List<string> toBeDeleted)
        {
            var stats = new MigrationStats();

            if (originalFailedMessage.ProcessingAttempts.Any(x => x.MessageMetadata.ContainsKey(SplitFromUniqueMessageIdHeader)))
            {
                return stats;
            }

            var originalStatus = originalFailedMessage.Status;

            stats.Checked = 1;

            var processingAttempts = originalFailedMessage.ProcessingAttempts
                .Select((a, i) => new ProcessingAttemptRecord(a, i))
                .ToArray();

            //Split the original FailedMessage into separate documents based on new unique message id
            var failedMessages = processingAttempts
                .GroupBy(p => p.UniqueMessageId)
                .Select(g => new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(g.Key),
                    UniqueMessageId = g.Key,
                    ProcessingAttempts = g.OrderBy(a => a.Index).Select(a => a.Attempt).ToList(),
                    Status = FailedMessageStatus.Unresolved
                }).ToList();

            //Do nothing if we don't split the document
            if (failedMessages.Count == 1) return stats;

            stats.FoundProblem++;

            if (failedMessages.All(f => f.UniqueMessageId != originalFailedMessage.UniqueMessageId))
            {
                toBeDeleted.Add(session.Advanced.GetDocumentId(originalFailedMessage));
            }
            else
            {
                var failedMessageCopy = failedMessages.Single(f => f.UniqueMessageId == originalFailedMessage.UniqueMessageId);
                failedMessages.Remove(failedMessageCopy);
                failedMessages.Add(originalFailedMessage);

                originalFailedMessage.ProcessingAttempts = failedMessageCopy.ProcessingAttempts;
                originalFailedMessage.Status = failedMessageCopy.Status;
            }

            failedMessages.ForEach(failedMessage =>
            {
                var lastAttempt = failedMessage.ProcessingAttempts.Last();

                var messageType = GetMessageType(lastAttempt) ?? "Unknown Message Type";

                failedMessage.FailureGroups = CreateFailureGroups(messageType, lastAttempt).ToList();

                if (failedMessage.UniqueMessageId == originalFailedMessage.UniqueMessageId) return;

                foreach (var processingAttempt in failedMessage.ProcessingAttempts)
                {
                    processingAttempt.MessageMetadata[SplitFromUniqueMessageIdHeader] = originalFailedMessage.UniqueMessageId;
                    processingAttempt.MessageMetadata[OriginalStatusHeader] = originalStatus;
                }

                session.Store(failedMessage);
                stats.Created++;
            });

            return stats;
        }

        private IEnumerable<FailedMessage.FailureGroup> CreateFailureGroups(string messageType, FailedMessage.ProcessingAttempt attempt)
        {
            var failureDetails = new ClassifiableMessageDetails(messageType, attempt.FailureDetails, attempt);

            foreach (var classifier in classifiers)
            {
                var failureGroupTitle = classifier.ClassifyFailure(failureDetails);
                if (failureGroupTitle == null)
                {
                    continue;
                }

                var prefix = string.Format(GroupPrefixFormat, attempt.Headers.ProcessingEndpointName());

                var fullTitle = $"{prefix}{failureGroupTitle}";
                yield return new FailedMessage.FailureGroup
                {
                    Id = DeterministicGuid.MakeId(classifier.Name, fullTitle).ToString(),
                    Title = fullTitle,
                    Type = classifier.Name
                };
            }
        }

        private static string GetMessageType(FailedMessage.ProcessingAttempt processingAttempt)
        {
            object messageType;
            if (processingAttempt.MessageMetadata.TryGetValue("MessageType", out messageType))
            {
                return messageType as string;
            }
            return null;
        }

        class ProcessingAttemptRecord
        {
            public ProcessingAttemptRecord(FailedMessage.ProcessingAttempt attempt, int index)
            {
                Attempt = attempt;
                Index = index;

                var headers = new Dictionary<string, string>(attempt.Headers);

                UniqueMessageId = NewUniqueId(headers);

                IsRetry = headers.ContainsKey("ServiceControl.Retry.UniqueMessageId");
            }

            static string NewUniqueId(IReadOnlyDictionary<string, string> headers)
            {
                return DeterministicGuid.MakeId(headers.MessageId(), headers.ProcessingEndpointName()).ToString();
            }

            public FailedMessage.ProcessingAttempt Attempt { get; }
            public string UniqueMessageId { get; }
            public int Index { get; }
            public bool IsRetry { get; }
        }

        struct MigrationStats
        {
            public int Checked { get; set; }
            public int FoundProblem { get; set; }
            public int Created { get; set; }
            public int Deleted { get; set; }

            public static MigrationStats operator +(MigrationStats left, MigrationStats right) => new MigrationStats
            {
                Checked = left.Checked + right.Checked,
                Created = left.Created + right.Created,
                FoundProblem = left.FoundProblem + right.FoundProblem,
                Deleted = left.Deleted + right.Deleted
            };
        }

        public string MigrationId { get; } = "Split Failed Message Documents Once Again";

        public const int PageSize = 1024;
        public const string SplitFromUniqueMessageIdHeader = "CollapsedSubscribers.SplitFromUniqueMessageId";
        public const string OriginalStatusHeader = "CollapsedSubscribers.OriginalStatus";
        public const string GroupPrefixFormat = "Issue 842 - {0} - ";
    }
}