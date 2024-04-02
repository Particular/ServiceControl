namespace ServiceControl.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageFailures;
    using Persistence.RavenDB;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Session;

    class UnarchiveDocumentManager
    {
        public Task<UnarchiveOperation> LoadUnarchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType) => session.LoadAsync<UnarchiveOperation>(UnarchiveOperation.MakeId(groupId, archiveType));

        public async Task<UnarchiveOperation> CreateUnarchiveOperation(IAsyncDocumentSession session, string groupId, ArchiveType archiveType, int numberOfMessages, string groupName, int batchSize)
        {
            var operation = new UnarchiveOperation
            {
                Id = UnarchiveOperation.MakeId(groupId, archiveType),
                RequestId = groupId,
                ArchiveType = archiveType,
                TotalNumberOfMessages = numberOfMessages,
                NumberOfMessagesUnarchived = 0,
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
                .Where(failure => failure.Status == FailedMessageStatus.Archived)
                .Select(document => document.Id);

            var docs = await StreamResults(session, docQuery);

            var batches = docs
                .GroupBy(d => documentCount++ / batchSize);

            foreach (var batch in batches)
            {
                var unarchiveBatch = new UnarchiveBatch
                {
                    Id = UnarchiveBatch.MakeId(groupId, archiveType, batch.Key),
                    DocumentIds = batch.ToList()
                };

                await session.StoreAsync(unarchiveBatch);
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

        public Task<UnarchiveBatch> GetUnarchiveBatch(IAsyncDocumentSession session, string unUnarchiveOperationId, int batchNumber)
        {
            return session.LoadAsync<UnarchiveBatch>($"{unUnarchiveOperationId}/{batchNumber}");
        }

        public async Task<GroupDetails> GetGroupDetails(IAsyncDocumentSession session, string groupId)
        {
            var group = await session.Query<FailureGroupView, ArchivedGroupsViewIndex>()
                .FirstOrDefaultAsync(x => x.Id == groupId);

            return new GroupDetails
            {
                NumberOfMessagesInGroup = group?.Count ?? 0,
                GroupName = group?.Title ?? "Undefined"
            };
        }

        public void UnarchiveMessageGroupBatch(IAsyncDocumentSession session, UnarchiveBatch batch, ExpirationManager expirationManager)
        {
            // https://ravendb.net/docs/article-page/5.4/Csharp/client-api/operations/patching/single-document#remove-property
            var patchRequest = new PatchRequest
            {
                Script = @"this.Status = args.Status;",
                Values =
                {
                    { "Status", (int)FailedMessageStatus.Unresolved }
                }
            };

            expirationManager.CancelExpiration(patchRequest);

            var patchCommands = batch?.DocumentIds.Select(documentId => new PatchCommandData(documentId, null, patchRequest));

            if (patchCommands != null)
            {
                session.Advanced.Defer(patchCommands.ToArray<ICommandData>());
                session.Advanced.Defer(new DeleteCommandData(batch.Id, null));
            }
        }

        public async Task<bool> WaitForIndexUpdateOfUnarchiveOperation(IRavenSessionProvider sessionProvider, string requestId, TimeSpan timeToWait)
        {
            using var session = sessionProvider.OpenSession();
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

        public async Task UpdateUnarchiveOperation(IAsyncDocumentSession session, UnarchiveOperation unarchiveOperation)
        {
            await session.StoreAsync(unarchiveOperation);
        }

        public async Task RemoveUnarchiveOperation(IRavenSessionProvider sessionProvider, UnarchiveOperation unarchiveOperation)
        {
            using var session = sessionProvider.OpenSession();
            session.Advanced.Defer(new DeleteCommandData(unarchiveOperation.Id, null));
            await session.SaveChangesAsync();
        }

        public class GroupDetails
        {
            public string GroupName { get; set; }
            public int NumberOfMessagesInGroup { get; set; }
        }
    }
}