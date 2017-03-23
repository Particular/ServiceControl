namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus.Logging;
    using Raven.Client;
    using System.Linq;
    using ServiceControl.MessageFailures;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using System.Collections.Generic;

    public class ArchiveDocumentManager
    {
        private static ILog logger = LogManager.GetLogger<ArchiveDocumentManager>();

        public ArchiveOperation LoadArchiveOperation(IDocumentSession session, string groupId, ArchiveType archiveType)
        {
            return session.Load<ArchiveOperation>(ArchiveOperation.MakeId(groupId, archiveType));
        }

        public ArchiveOperation CreateArchiveOperation(IDocumentSession session, string groupId, ArchiveType archiveType, DateTime? cutOff, int numberOfMessages, string groupName, int batchSize)
        {
            var operation = new ArchiveOperation
            {
                Id = ArchiveOperation.MakeId(groupId, archiveType),
                RequestId = groupId,
                ArchiveType = archiveType,
                TotalNumberOfMessages = numberOfMessages,
                NumberOfMessagesArchived = 0,
                Started = DateTime.Now,
                GroupName = groupName,
                NumberOfBatches = (int)Math.Ceiling(numberOfMessages / (float)batchSize),
                CurrentBatch = 0
            };

            session.Store(operation);

            int documentCount = 0;
            var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName);

            if (cutOff.HasValue)
            {
                indexQuery = indexQuery.Customize(x => x.WaitForNonStaleResultsAsOf(cutOff.Value));
            }

            var docQuery = indexQuery
                .Where(failure => failure.FailureGroupId == groupId)
                .Where(failure => failure.Status == FailedMessageStatus.Unresolved)
                .AsProjection<FailureGroupMessageView>()
                .Select(document => document.Id);

            var docs = StreamResults(session, docQuery).ToArray();

            var documentsToArchive = docs
                .GroupBy(d =>
                {
                    return documentCount++ / batchSize;
                });

            foreach (var batch in documentsToArchive)
            {
                var archiveBatch = new ArchiveOperationBatch
                {
                    Id = ArchiveOperationBatch.MakeId(groupId, archiveType, batch.Key),
                    DocumentIds = batch.ToList()
                };

                session.Store(archiveBatch);
            }

            return operation;
        }

        IEnumerable<string> StreamResults(IDocumentSession session, IQueryable<string> query)
        {
            using (var enumerator = session.Advanced.Stream(query))
            {
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current.Document;
                }
            }
        }

        public ArchiveOperationBatch GetArchiveBatch(IDocumentSession session, string archiveOperationId, int batchNumber)
        {
            return session.Load<ArchiveOperationBatch>($"{archiveOperationId}/{batchNumber}");
        }

        public GroupDetails GetGroupDetails(IDocumentSession session, string groupId)
        {
            var group = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                .FirstOrDefault(x => x.Id == groupId);

            return new GroupDetails
            {
                NumberOfMessagesInGroup = group.Count,
                GroupName = group.Title ?? "Undefined"
            };
        }

        public void ArchiveMessageGroupBatch(IDocumentSession session, ArchiveOperationBatch batch)
        {
            var patchCommands = batch?.DocumentIds
                .Select(documentId =>
                new PatchCommandData
                {
                    Key = documentId,
                    Patches = new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set,
                            Name = "Status",
                            Value = (int) FailedMessageStatus.Archived
                        }
                    }
                });

            if (patchCommands != null)
            {
                session.Advanced.DocumentStore.DatabaseCommands.Batch(patchCommands);
                session.Advanced.DocumentStore.DatabaseCommands.Delete(batch.Id, null);
            }
        }

        public void UpdateArchiveOperation(IDocumentSession session, ArchiveOperation archiveOperation)
        {
            session.Store(archiveOperation);
        }

        public void RemoveArchiveOperation(IDocumentStore store, ArchiveOperation archiveOperation)
        {
            using (var session = store.OpenSession())
            {
                session.Advanced.DocumentStore.DatabaseCommands.Delete(archiveOperation.Id, null);
                session.SaveChanges();
            }
        }

        public class GroupDetails
        {
            public string GroupName { get; set; }
            public int NumberOfMessagesInGroup { get; set; }
        }
    }
}
