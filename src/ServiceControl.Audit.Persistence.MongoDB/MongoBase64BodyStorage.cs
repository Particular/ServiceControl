namespace ServiceControl.Audit.Persistence.MongoDB
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Collections;
    using Documents;
    using global::MongoDB.Driver;

    class MongoBase64BodyStorage(IMongoClientProvider clientProvider) : IBodyStorage
    {
        // Store is not used - bodies are written directly by the unit of work during ingestion
        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
            => throw new NotSupportedException("Bodies are stored by the unit of work during ingestion");

        public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database.GetCollection<MessageBodyDocument>(CollectionNames.MessageBodies);

            var filter = Builders<MessageBodyDocument>.Filter.Eq(d => d.Id, bodyId);
            var document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (document == null)
            {
                return new StreamResult { HasResult = false };
            }

            var bodyBytes = Convert.FromBase64String(document.Body);

            return new StreamResult
            {
                HasResult = true,
                Stream = new MemoryStream(bodyBytes),
                ContentType = document.ContentType,
                BodySize = document.BodySize,
                Etag = document.Id
            };
        }
    }
}
