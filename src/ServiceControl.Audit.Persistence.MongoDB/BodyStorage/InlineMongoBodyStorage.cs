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
    /// Body storage implementation that stores message bodies inline in the ProcessedMessages collection.
    /// </summary>
    class InlineMongoBodyStorage(IMongoClientProvider clientProvider) : IBodyStorage
    {
        public async Task<StreamResult> TryFetch(string bodyId, CancellationToken cancellationToken)
        {
            var collection = clientProvider.Database
                .GetCollection<ProcessedMessageDocument>(CollectionNames.ProcessedMessages);

            var filter = Builders<ProcessedMessageDocument>.Filter.Eq(d => d.Id, bodyId);
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
                ContentType = document.BodyContentType ?? "text/plain",
                BodySize = document.BodySize,
                Etag = bodyId
            };
        }

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
