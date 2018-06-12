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
    using System.Threading.Tasks;

    public class ArchiveDocumentManager
    {
        private static ILog logger = LogManager.GetLogger<ArchiveDocumentManager>();

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

            int documentCount = 0;
            var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName);

            var docQuery = indexQuery
                .Where(failure => failure.FailureGroupId == groupId)
                .Where(failure => failure.Status == FailedMessageStatus.Unresolved)
                .ProjectFromIndexFieldsInto<FailureGroupMessageView>()
                .Select(document => document.Id);

            var docs = await StreamResults(session, docQuery);

            var batches = docs
                .GroupBy(d =>
                {
                    return documentCount++ / batchSize;
                });

            foreach (var batch in batches)
            {
                var archiveBatch = new ArchiveBatch
                {
                    Id = ArchiveBatch.MakeId(groupId, archiveType, batch.Key),
                    DocumentIds = batch.ToList()
                };

                await session.StoreAsync(archiveBatch);
            }

            return operation;
        }

        async Task<IEnumerable<string>> StreamResults(IAsyncDocumentSession session, IQueryable<string> query)
        {
            var results = new List<string>();
            using (var enumerator = await session.Advanced.StreamAsync(query).ConfigureAwait(false))
            {
                while (await enumerator.MoveNextAsync())
                {
                    results.Add(enumerator.Current.Document);
                }
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
                .FirstOrDefaultAsync(x => x.Id == groupId);

            return new GroupDetails
            {
                NumberOfMessagesInGroup = group.Count,
                GroupName = group.Title ?? "Undefined"
            };
        }

        public async Task ArchiveMessageGroupBatch(IAsyncDocumentSession session, ArchiveBatch batch)
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
                await session.Advanced.DocumentStore.AsyncDatabaseCommands.BatchAsync(patchCommands)
                    .ConfigureAwait(false);
                await session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(batch.Id, null)
                    .ConfigureAwait(false);
            }
        }

        public async Task<bool> WaitForIndexUpdateOfArchiveOperation(IDocumentStore store, string requestId, ArchiveType archiveType, TimeSpan timeToWait)
        {
            using (var session = store.OpenAsyncSession())
            {
                var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName)
                    .Customize(x => x.WaitForNonStaleResultsAsOfNow(timeToWait));

                var docQuery = indexQuery
                    .Where(failure => failure.FailureGroupId == requestId)
                    .ProjectFromIndexFieldsInto<FailureGroupMessageView>();

                try
                {
                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
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

        public async Task RemoveArchiveOperation(IDocumentStore store, ArchiveOperation archiveOperation)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(archiveOperation.Id, null)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        public class GroupDetails
        {
            public string GroupName { get; set; }
            public int NumberOfMessagesInGroup { get; set; }
        }
    }
}
