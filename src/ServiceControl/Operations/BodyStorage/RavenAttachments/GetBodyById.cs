namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using CompositeViews.Messages;
    using System.Text;
    using Nancy;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetBodyById : BaseModule
    {

        public GetBodyById()
        {
            Get["/messages/{id*}/body", true] = async (parameters, token) =>
            {
                string messageId = parameters.id;
                messageId = messageId?.Replace("/", @"\");
                Action<Stream> contents;
                string contentType;
                int bodySize;
                var attachment = await Store.AsyncDatabaseCommands.GetAttachmentAsync("messagebodies/" + messageId).ConfigureAwait(false);
                Etag currentEtag;

                if (attachment == null)
                {
                    using (var session = Store.OpenAsyncSession())
                    {
                        RavenQueryStatistics stats;
                        var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                            .Statistics(out stats)
                            .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
                            .FirstOrDefaultAsync(f => f.MessageId == messageId)
                            .ConfigureAwait(false);

                        if (message == null)
                        {
                            return HttpStatusCode.NotFound;
                        }

                        if (message.BodyNotStored)
                        {
                            return HttpStatusCode.NoContent;
                        }

                        if (message.Body == null)
                        {
                            return HttpStatusCode.NotFound;
                        }

                        var data = Encoding.UTF8.GetBytes(message.Body);
                        contents = stream => stream.Write(data, 0, data.Length);
                        contentType = message.ContentType;
                        bodySize = message.BodySize;
                        currentEtag = stats.IndexEtag;
                    }
                }
                else
                {
                    contents = stream => attachment.Data().CopyTo(stream);
                    contentType = attachment.Metadata["ContentType"].Value<string>();
                    bodySize = attachment.Metadata["ContentLength"].Value<int>();
                    currentEtag = attachment.Etag;
                }

                return new Response { Contents = contents }
                    .WithContentType(contentType)
                    .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                    .WithHeader("Content-Length", bodySize.ToString())
                    .WithHeader("ETag", currentEtag)
                    .WithStatusCode(HttpStatusCode.OK);
            };
        }

    }

}