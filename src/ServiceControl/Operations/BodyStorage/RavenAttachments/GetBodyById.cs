namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using System.Linq;
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
            Get["/messages/{id*}/body"] = parameters =>
            {
                string messageId = parameters.id;
                if (messageId != null)
                {
                    messageId = messageId.Replace("/", @"\");
                }
                Action<Stream> contents;
                string contentType;
                int bodySize;
                var attachment = Store.DatabaseCommands.GetAttachment("messagebodies/" + messageId);
                Etag currentEtag;

                if (attachment == null)
                {
                    using (var session = Store.OpenSession())
                    {
                        RavenQueryStatistics stats;
                        var message = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                            .Statistics(out stats)
                            .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
                            .FirstOrDefault(f => f.MessageId == messageId);

                        if (message == null)
                        {
                            return HttpStatusCode.NotFound;
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