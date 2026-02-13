namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Collections;
    using Documents;
    using global::MongoDB.Driver;

    /// <summary>
    /// Reads message bodies from the messageBodies collection.
    /// Bodies are written asynchronously by BodyStorageWriter via a channel.
    /// </summary>
    class MongoBodyStorage(IMongoClientProvider clientProvider) : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
        {
            // Bodies are written by BodyStorageWriter, not through IBodyStorage.Store()
            return Task.CompletedTask;
        }

        public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database
                .GetCollection<MessageBodyDocument>(CollectionNames.MessageBodies);

            var filter = Builders<MessageBodyDocument>.Filter.Eq(d => d.Id, bodyId);
            var document = await collection.Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (document == null)
            {
                return new StreamResult { HasResult = false };
            }

            byte[] bodyBytes;
            if (document.TextBody != null)
            {
                bodyBytes = System.Text.Encoding.UTF8.GetBytes(document.TextBody);
            }
            else if (document.BinaryBody != null)
            {
                bodyBytes = document.BinaryBody;
            }
            else
            {
                return new StreamResult { HasResult = false };
            }

            return new StreamResult
            {
                HasResult = true,
                Stream = new MemoryStream(bodyBytes),
                ContentType = document.ContentType ?? "text/plain",
                BodySize = document.BodySize,
                Etag = bodyId
            };
        }
    }
}
