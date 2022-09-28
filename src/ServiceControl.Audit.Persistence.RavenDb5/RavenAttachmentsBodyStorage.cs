namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.IO;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Raven.Client.Documents;
    using Raven.Client.Documents.BulkInsert;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        readonly IDocumentStore documentStore;
        readonly BulkInsertOperation bulkInsertOperation;
        readonly int settingsMaxBodySizeToStore;

        public RavenAttachmentsBodyStorage(IDocumentStore documentStore, BulkInsertOperation bulkInsert, int settingsMaxBodySizeToStore)
        {
            this.documentStore = documentStore;
            bulkInsertOperation = bulkInsert;
            this.settingsMaxBodySizeToStore = settingsMaxBodySizeToStore;
        }

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            if (bodySize > settingsMaxBodySizeToStore)
            {
                return Task.CompletedTask;
            }

            return bulkInsertOperation.AttachmentsFor(bodyId)
                .StoreAsync("body", bodyStream, contentType);
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var result = await session.Advanced.Attachments.GetAsync($"message/{bodyId}", "body")
                    .ConfigureAwait(false);

                if (result == null)
                {
                    return new StreamResult { HasResult = false };
                }

                return new StreamResult
                {
                    HasResult = true,
                    Stream = result.Stream,
                    BodySize = (int)result.Details.Size,
                    ContentType = result.Details.ContentType,
                    Etag = result.Details.ChangeVector
                };
            }
        }
    }
}