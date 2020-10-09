namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Session;
    using Sparrow.Json.Parsing;

    class ArchiveDocumentManager
    {
        public Task<ArchiveOperation> LoadArchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType)
        {
            return session.LoadAsync<ArchiveOperation>(ArchiveOperation.MakeId(groupId, archiveType));
        }

        public async Task<ArchiveOperation> CreateArchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType, int numberOfMessages, string groupName, int batchSize)
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

            await session.StoreAsync(operation).ConfigureAwait(false);

            var documentCount = 0;
            var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName);

            var docQuery = indexQuery
                .Where(failure => failure.FailureGroupId == groupId)
                .Where(failure => failure.Status == FailedMessageStatus.Unresolved)
                .Select(document => document.Id);

            var docs = await StreamResults(session, docQuery).ConfigureAwait(false);

            var batches = docs
                .GroupBy(d => documentCount++ / batchSize);

            foreach (var batch in batches)
            {
                var archiveBatch = new ArchiveBatch
                {
                    Id = ArchiveBatch.MakeId(groupId, archiveType, batch.Key),
                    DocumentIds = batch.ToList()
                };

                await session.StoreAsync(archiveBatch).ConfigureAwait(false);
            }

            return operation;
        }

        async Task<IEnumerable<string>> StreamResults(IAsyncDocumentSession session, IQueryable<string> query)
        {
            var results = new List<string>();
            var enumerator = await session.Advanced.StreamAsync(query).ConfigureAwait(false);

            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                results.Add(enumerator.Current.Document);
            }
            return results;
        }

        public Task<ArchiveBatch> GetArchiveBatch(IAsyncDocumentSession session, string archiveOperationId, int batchNumber)
        {
            return session.LoadAsync<ArchiveBatch>($"{archiveOperationId}/{batchNumber}");
        }

        public async Task<GroupDetails> GetGroupDetails(IAsyncDocumentSession session, string groupId)
        {
            var group = await session.Query<FailureGroupView, FailureGroupsViewIndex>()
                .FirstOrDefaultAsync(x => x.Id == groupId)
                .ConfigureAwait(false);

            return new GroupDetails
            {
                NumberOfMessagesInGroup = group?.Count ?? 0,
                GroupName = group?.Title ?? "Undefined"
            };
        }

        public void ArchiveMessageGroupBatch(IAsyncDocumentSession session, ArchiveBatch batch)
        {
            if (batch == null)
            {
                return;
            }

            if (batch.DocumentIds.Any())
            {
                var command = new BatchPatchCommandData(batch.DocumentIds, new PatchRequest
                {
                    Script = "this.Status = $newStatus",
                    Values = new Dictionary<string, object>
                    {
                        ["newStatus"] = (int)FailedMessageStatus.Archived
                    }
                }, null);

                session.Advanced.Defer(command);
            }

            session.Delete(batch.Id);
        }

        public async Task<bool> WaitForIndexUpdateOfArchiveOperation(IDocumentStore store, string requestId, ArchiveType archiveType, TimeSpan timeToWait)
        {
            using (var session = store.OpenAsyncSession())
            {
                var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName)
                    .Customize(x => x.WaitForNonStaleResults(timeToWait));

                var docQuery = indexQuery
                    .Where(failure => failure.FailureGroupId == requestId)
                    .Select(document => document.Id);

                try
                {
                    await docQuery.AnyAsync().ConfigureAwait(false);

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public Task UpdateArchiveOperation(IAsyncDocumentSession session, ArchiveOperation archiveOperation)
        {
            return session.StoreAsync(archiveOperation);
        }

        public void RemoveArchiveOperation(IDocumentStore store, ArchiveOperation archiveOperation)
        {
            using (var session = store.OpenAsyncSession())
            {
                session.Delete(archiveOperation.Id);
            }
        }

        // static PatchRequest[] patchRequest =
        // {
        //     new PatchRequest
        //     {
        //         Type = PatchCommandType.Set,
        //         Name = "Status",
        //         Value = (int)FailedMessageStatus.Archived
        //     }
        // };
        public class GroupDetails
        {
            public string GroupName { get; set; }
            public int NumberOfMessagesInGroup { get; set; }
        }
    }
}