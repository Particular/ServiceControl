namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Json.Linq;

    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public IDocumentStore DocumentStore { get; set; }

        public async Task<string> Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            await DocumentStore.AsyncDatabaseCommands.PutAttachmentAsync($"messagebodies/{bodyId}", null, bodyStream, new RavenJObject
#pragma warning restore 618
            {
                {"ContentType", contentType},
                {"ContentLength", bodySize}
            }).ConfigureAwait(false);

            return $"/messages/{bodyId}/body";
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            //We want to continue using attachments for now
#pragma warning disable 618
            var attachment = await DocumentStore.AsyncDatabaseCommands.GetAttachmentAsync($"messagebodies/{bodyId}").ConfigureAwait(false);
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