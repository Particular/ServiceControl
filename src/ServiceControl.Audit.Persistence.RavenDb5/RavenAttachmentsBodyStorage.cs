namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.IO;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Raven.Client.Documents.BulkInsert;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public RavenAttachmentsBodyStorage(IRavenDbSessionProvider sessionProvider, BulkInsertOperation bulkInsert, int settingsMaxBodySizeToStore)
        {
            this.sessionProvider = sessionProvider;
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
            using (var session = sessionProvider.OpenSession())
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

        readonly IRavenDbSessionProvider sessionProvider;
        readonly BulkInsertOperation bulkInsertOperation;
        readonly int settingsMaxBodySizeToStore;
    }
}