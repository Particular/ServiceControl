namespace ServiceControl.Audit.Persistence.MongoDB.BodyStorage
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Collections;
    using Documents;
    using global::MongoDB.Driver;
    using NServiceBus;

    /// <summary>
    /// Reads message bodies stored inline in the ProcessedMessages collection.
    /// Text bodies are stored as UTF-8 strings in the Body field (searchable).
    /// Binary bodies are stored as BSON BinData in the BinaryBody field (not searchable).
    /// This storage does not implement Store() as bodies are written directly
    /// by MongoAuditIngestionUnitOfWork.
    /// </summary>
    class InlineBodyStorage(IMongoClientProvider clientProvider) : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
        {
            // Bodies are stored inline by MongoAuditIngestionUnitOfWork, not through IBodyStorage.Store()
            // This method should not be called for inline storage
            return Task.CompletedTask;
        }

        public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database.GetCollection<ProcessedMessageDocument>(CollectionNames.ProcessedMessages);

            // Query for the document with the body (text or binary)
            var filter = Builders<ProcessedMessageDocument>.Filter.Eq(d => d.Id, bodyId);
            var projection = Builders<ProcessedMessageDocument>.Projection
                .Include(d => d.Body)
                .Include(d => d.BinaryBody)
                .Include(d => d.Headers);

            var document = await collection.Find(filter)
                .Project<ProcessedMessageDocument>(projection)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            // Check for text body first, then binary body
            byte[] bodyBytes;
            if (document?.Body != null)
            {
                bodyBytes = System.Text.Encoding.UTF8.GetBytes(document.Body);
            }
            else if (document?.BinaryBody != null)
            {
                bodyBytes = document.BinaryBody;
            }
            else
            {
                return new StreamResult { HasResult = false };
            }

            // Get content type from headers
            var contentType = document.Headers?.GetValueOrDefault(Headers.ContentType, "text/plain") ?? "text/plain";

            return new StreamResult
            {
                HasResult = true,
                Stream = new MemoryStream(bodyBytes),
                ContentType = contentType,
                BodySize = bodyBytes.Length,
                Etag = document.Id
            };
        }
    }
}
