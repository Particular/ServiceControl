namespace ServiceControl.Operations.BodyStorage.RavenAttachments
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence;

    // For Raven5, look at how the Audit instance is implementing this, as Attachments won't exist
    // and there will be no need for a fallback method on a new persistence
    class RavenAttachmentsBodyStorage : IBodyStorage
    {
        public RavenAttachmentsBodyStorage(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        IDocumentStore documentStore;

        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
        {
            var id = MessageBodyIdGenerator.MakeDocumentId(bodyId);
            //We want to continue using attachments for now
#pragma warning disable 618
            return documentStore.AsyncDatabaseCommands.PutAttachmentAsync(id, null, bodyStream,
                new RavenJObject
#pragma warning restore 618
                {
                    {"ContentType", contentType},
                    {"ContentLength", bodySize}
                });
        }

        // The RavenDB5 implementation, like the Audit instance, will not use Attachments
        public async Task<MessageBodyStreamResult> TryFetch(string bodyId)
        {
            bodyId = bodyId?.Replace("/", @"\");

            // First try loading from index 

            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Statistics(out var stats)
                    .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
                    .FirstOrDefaultAsync(f => f.MessageId == bodyId);

                if (message != null)
                {
                    if (message.Body == null && message.BodyNotStored)
                    {
                        return new MessageBodyStreamResult { HasResult = false };
                    }

                    if (message.Body != null)
                    {
                        var stream = new MemoryStream();
                        var writer = new StreamWriter(stream);
                        writer.Write(message.Body);
                        writer.Flush();
                        stream.Position = 0;

                        return new MessageBodyStreamResult
                        {
                            HasResult = true,
                            Stream = stream,
                            ContentType = message.ContentType,
                            BodySize = message.BodySize,
                            Etag = stats.IndexEtag
                        };
                    }
                }
            }

            var id = MessageBodyIdGenerator.MakeDocumentId(bodyId);

            //We want to continue using attachments for now
#pragma warning disable 618
            var attachment = await documentStore.AsyncDatabaseCommands.GetAttachmentAsync(id);
#pragma warning restore 618

            if (attachment != null)
            {
                return new MessageBodyStreamResult
                {
                    HasResult = true,
                    Stream = (MemoryStream)attachment.Data(),
                    ContentType = attachment.Metadata["ContentType"].Value<string>(),
                    BodySize = attachment.Metadata["ContentLength"].Value<int>(),
                    Etag = attachment.Etag
                };
            }

            return new MessageBodyStreamResult();
        }
    }
}