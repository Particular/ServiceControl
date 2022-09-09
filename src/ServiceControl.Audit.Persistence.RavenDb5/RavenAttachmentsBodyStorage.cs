namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.IO;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Raven.Client.Documents;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        readonly IDocumentStore documentStore;

        public RavenAttachmentsBodyStorage(IDocumentStore documentStore) => this.documentStore = documentStore;

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                session.Advanced.Attachments.Store($"message/{bodyId}", "body", bodyStream, contentType);
            }
            return Task.CompletedTask;
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