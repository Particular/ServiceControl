namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Collections;
    using Documents;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;

    class MongoFailedAuditStorage(IMongoClientProvider clientProvider) : IFailedAuditStorage
    {
        IMongoCollection<FailedAuditImportDocument> GetCollection() =>
            clientProvider.Database.GetCollection<FailedAuditImportDocument>(CollectionNames.FailedAuditImports);

        public async Task SaveFailedAuditImport(FailedAuditImport message)
        {
            var document = new FailedAuditImportDocument
            {
                Id = ObjectId.GenerateNewId(),
                MessageId = message.Message.Id,
                Headers = message.Message.Headers,
                Body = message.Message.Body,
                ExceptionInfo = message.ExceptionInfo
            };

            await GetCollection().InsertOneAsync(document).ConfigureAwait(false);
        }

        public async Task ProcessFailedMessages(
            Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
            CancellationToken cancellationToken)
        {
            var collection = GetCollection();
            var documentsToDelete = new List<ObjectId>();

            using var cursor = await collection.FindAsync(
                FilterDefinition<FailedAuditImportDocument>.Empty,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var document in cursor.Current)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var transportMessage = new FailedTransportMessage
                    {
                        Id = document.MessageId,
                        Headers = document.Headers,
                        Body = document.Body
                    };

                    var documentId = document.Id;

                    await onMessage(
                        transportMessage,
                        _ =>
                        {
                            documentsToDelete.Add(documentId);
                            return Task.CompletedTask;
                        },
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (documentsToDelete.Count > 0)
            {
                var filter = Builders<FailedAuditImportDocument>.Filter.In(d => d.Id, documentsToDelete);
                _ = await collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<int> GetFailedAuditsCount()
        {
            var count = await GetCollection().CountDocumentsAsync(FilterDefinition<FailedAuditImportDocument>.Empty).ConfigureAwait(false);
            return (int)count;
        }
    }
}
