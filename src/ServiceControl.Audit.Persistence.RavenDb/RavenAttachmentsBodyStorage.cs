namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.Audit.Auditing.BodyStorage;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        readonly IDocumentStore documentStore;

        public RavenAttachmentsBodyStorage(IDocumentStore documentStore) => this.documentStore = documentStore;

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            return documentStore.AsyncDatabaseCommands.PutAttachmentAsync($"messagebodies/{bodyId}", null, bodyStream,
                new RavenJObject
#pragma warning restore 618
                {
                    {"ContentType", contentType},
                    {"ContentLength", bodySize}
                });
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            var attachment = await documentStore.AsyncDatabaseCommands.GetAttachmentAsync($"messagebodies/{bodyId}").ConfigureAwait(false);
#pragma warning restore 618

            return attachment == null
                ? new StreamResult
                {
                    HasResult = false,
                    Stream = null
                }
                : new StreamResult
                {
                    HasResult = true,
                    Stream = attachment.Data(),
                    ContentType = attachment.Metadata["ContentType"].Value<string>(),
                    BodySize = attachment.Metadata["ContentLength"].Value<int>(),
                    Etag = attachment.Etag
                };
        }
    }
}