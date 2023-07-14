namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class UnarchiveDocumentManager
    {
        public Task<UnarchiveOperation> LoadUnarchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType)
        {
            return session.LoadAsync<UnarchiveOperation>(UnarchiveOperation.MakeId(groupId, archiveType));
        }

        public async Task<UnarchiveOperation> CreateUnarchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType, int numberOfMessages, string groupName, int batchSize)
        {
            var operation = new UnarchiveOperation
            {
                Id = UnarchiveOperation.MakeId(groupId, archiveType),
                RequestId = groupId,
                ArchiveType = archiveType,
                TotalNumberOfMessages = numberOfMessages,
                NumberOfMessagesUnarchived = 0,
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
                .Where(failure => failure.Status == FailedMessageStatus.Archived)
                .Select(document => document.Id);

            var docs = await StreamResults(session, docQuery).ConfigureAwait(false);

            var batches = docs
                .GroupBy(d => documentCount++ / batchSize);

            foreach (var batch in batches)
            {
                var unarchiveBatch = new UnarchiveBatch
                {
                    Id = UnarchiveBatch.MakeId(groupId, archiveType, batch.Key),
                    DocumentIds = batch.ToList()
                };

                await session.StoreAsync(unarchiveBatch).ConfigureAwait(false);
            }

            return operation;
        }

        async Task<IEnumerable<string>> StreamResults(IAsyncDocumentSession session, IQueryable<string> query)
        {
            var results = new List<string>();
            using (var enumerator = await session.Advanced.StreamAsync(query).ConfigureAwait(false))
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    results.Add(enumerator.Current.Document);
                }
            }

            return results;
        }

        public Task<UnarchiveBatch> GetUnarchiveBatch(IAsyncDocumentSession session, string unUnarchiveOperationId, int batchNumber)
        {
            return session.LoadAsync<UnarchiveBatch>($"{unUnarchiveOperationId}/{batchNumber}");
        }

        public async Task<GroupDetails> GetGroupDetails(IAsyncDocumentSession session, string groupId)
        {
            var group = await session.Query<FailureGroupView, ArchivedGroupsViewIndex>()
                .FirstOrDefaultAsync(x => x.Id == groupId)
                .ConfigureAwait(false);

            return new GroupDetails
            {
                NumberOfMessagesInGroup = group?.Count ?? 0,
                GroupName = group?.Title ?? "Undefined"
            };
        }

        public async Task UnarchiveMessageGroupBatch(IAsyncDocumentSession session, UnarchiveBatch batch)
        {
            var patchCommands = batch?.DocumentIds.Select(documentId => new PatchCommandData { Key = documentId, Patches = patchRequest });

            if (patchCommands != null)
            {
                await session.Advanced.DocumentStore.AsyncDatabaseCommands.BatchAsync(patchCommands)
                    .ConfigureAwait(false);
                await session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(batch.Id, null)
                    .ConfigureAwait(false);
            }
        }

        public async Task<bool> WaitForIndexUpdateOfUnarchiveOperation(IDocumentStore store, string requestId, TimeSpan timeToWait)
        {
            using (var session = store.OpenAsyncSession())
            {
                var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName)
                    .Customize(x => x.WaitForNonStaleResultsAsOfNow(timeToWait));

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

        public Task UpdateUnarchiveOperation(IAsyncDocumentSession session, UnarchiveOperation unarchiveOperation)
        {
            return session.StoreAsync(unarchiveOperation);
        }

        public async Task RemoveUnarchiveOperation(IDocumentStore store, UnarchiveOperation unarchiveOperation)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(unarchiveOperation.Id, null)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }

        static PatchRequest[] patchRequest =
        {
            new PatchRequest
            {
                Type = PatchCommandType.Set,
                Name = "Status",
                Value = (int)FailedMessageStatus.Unresolved
            }
        };

        public class GroupDetails
        {
            public string GroupName { get; set; }
            public int NumberOfMessagesInGroup { get; set; }
        }
    }
}