namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using Microsoft.Extensions.Logging;
    using Persistence.RavenDB;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Session;

    class ArchiveDocumentManager(ExpirationManager expirationManager, ILogger logger)
    {
        public Task<ArchiveOperation> LoadArchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType) => session.LoadAsync<ArchiveOperation>(ArchiveOperation.MakeId(groupId, archiveType));

        public async Task<ArchiveOperation> CreateArchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType, int numberOfMessages, string groupName, int batchSize)
        {
            var operation = new ArchiveOperation
            {
                Id = ArchiveOperation.MakeId(groupId, archiveType),
                RequestId = groupId,
                ArchiveType = archiveType,
                TotalNumberOfMessages = numberOfMessages,
                NumberOfMessagesArchived = 0,
                Started = DateTime.UtcNow,
                GroupName = groupName,
                NumberOfBatches = (int)Math.Ceiling(numberOfMessages / (float)batchSize),
                CurrentBatch = 0
            };

            await session.StoreAsync(operation);

            var documentCount = 0;
            var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName);

            var docQuery = indexQuery
                .Where(failure => failure.FailureGroupId == groupId)
                .Where(failure => failure.Status == FailedMessageStatus.Unresolved)
                .Select(document => document.Id);

            var docs = await StreamResults(session, docQuery);

            var batches = docs
                .GroupBy(d => documentCount++ / batchSize);

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
            await using var enumerator = await session.Advanced.StreamAsync(query);
            while (await enumerator.MoveNextAsync())
            {
                results.Add(enumerator.Current.Document);
            }

            return results;
        }

        public Task<ArchiveBatch> GetArchiveBatch(IAsyncDocumentSession session, string archiveOperationId, int batchNumber) => session.LoadAsync<ArchiveBatch>($"{archiveOperationId}/{batchNumber}");

        public async Task<GroupDetails> GetGroupDetails(IAsyncDocumentSession session, string groupId)
        {
            var group = await session.Query<FailureGroupView, FailureGroupsViewIndex>()
                .FirstOrDefaultAsync(x => x.Id == groupId);

            return new GroupDetails
            {
                NumberOfMessagesInGroup = group?.Count ?? 0,
                GroupName = group?.Title ?? "Undefined"
            };
        }

        public void ArchiveMessageGroupBatch(IAsyncDocumentSession session, ArchiveBatch batch)
        {
            var patchRequest = new PatchRequest
            {
                Script = "this.Status = args.Status;",
                Values =
                {
                    { "Status", (int)FailedMessageStatus.Archived }
                }
            };

            expirationManager.EnableExpiration(patchRequest);

            var patchCommands = batch?.DocumentIds.Select(documentId => new PatchCommandData(documentId, null, patchRequest));

            if (patchCommands != null)
            {
                session.Advanced.Defer(patchCommands.ToArray<ICommandData>());
                session.Advanced.Defer(new DeleteCommandData(batch.Id, null));
            }
        }

        public async Task<bool> WaitForIndexUpdateOfArchiveOperation(IRavenSessionProvider sessionProvider, string requestId, TimeSpan timeToWait)
        {
            using var session = await sessionProvider.OpenSession();
            var indexQuery = session.Query<FailureGroupMessageView>(new FailedMessages_ByGroup().IndexName)
                .Customize(x => x.WaitForNonStaleResults(timeToWait));

            var docQuery = indexQuery
                .Where(failure => failure.FailureGroupId == requestId)
                .Select(document => document.Id);

            try
            {
                await docQuery.AnyAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task UpdateArchiveOperation(IAsyncDocumentSession session, ArchiveOperation archiveOperation) => session.StoreAsync(archiveOperation);

        public async Task RemoveArchiveOperation(IRavenSessionProvider sessionProvider, ArchiveOperation archiveOperation)
        {
            using var session = await sessionProvider.OpenSession();
            session.Advanced.Defer(new DeleteCommandData(archiveOperation.Id, null));
            await session.SaveChangesAsync();

            logger.LogInformation("Removing ArchiveOperation {ArchiveOperationId} completed", archiveOperation.Id);
        }

        public class GroupDetails
        {
            public string GroupName { get; set; }
            public int NumberOfMessagesInGroup { get; set; }
        }

    }
}