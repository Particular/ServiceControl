namespace ServiceControl.Audit.Auditing.BodyStorage.RavenAttachments
{
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Document;
    using Raven.Json.Linq;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public async Task<string> Store(BulkInsertOperation bulkInsert, string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            await bulkInsert.DatabaseCommands.PutAttachmentAsync($"messagebodies/{bodyId}", null, bodyStream, new RavenJObject
#pragma warning restore 618
            {
                {"ContentType", contentType},
                {"ContentLength", bodySize}
            }).ConfigureAwait(false);

            return $"/messages/{bodyId}/body";
        }

        public async Task<StreamResult> TryFetch(IDocumentStore documentStore, string bodyId)
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