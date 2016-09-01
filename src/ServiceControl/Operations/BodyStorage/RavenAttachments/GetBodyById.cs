namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using System.Linq;
    using CompositeViews.Messages;
    using System.Text;
    using Nancy;
    using Nancy.Responses;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    
    public class GetBodyById : BaseModule
    {
        private readonly IBodyStorage bodyStorage;

        public GetBodyById(IBodyStorage bodyStorage)
        {
            this.bodyStorage = bodyStorage;
        }

        public GetBodyById()
        {
            Get["/messages/{id*}/body_v2"] = parameters =>
            {
                string messageId = parameters.id;
                messageId = messageId?.Replace("/", @"\");
                Stream stream;
                long contentLength;
                string contentType;

                if (bodyStorage.TryFetch(messageId, out stream, out contentType, out contentLength))
                {
                    return new StreamResponse(() => stream, contentType)
                        .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                        .WithHeader("Content-Length", contentLength.ToString())
                        .WithStatusCode(HttpStatusCode.OK);
                }

                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var message = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .Statistics(out stats)
                        .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
                        .FirstOrDefault(f => f.MessageId == messageId);

                    if (message == null)
                    {
                        return new Response().WithStatusCode(HttpStatusCode.NotFound);
                    }

                    if (message.BodyNotStored)
                    {
                        return new Response().WithStatusCode(HttpStatusCode.NoContent);
                    }

                    return new Response().WithStatusCode(HttpStatusCode.NotFound);
                }
            };

            Get["/messages/{id*}/body"] = parameters =>
            {
                string messageId = parameters.id;
                messageId = messageId?.Replace("/", @"\");
                Stream stream;
                long contentLength;
                string contentType;

                if (bodyStorage.TryFetch(messageId, out stream, out contentType, out contentLength))
                {
                    return new StreamResponse(() => stream, contentType)
                        .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                        .WithHeader("Content-Length", contentLength.ToString())
                        .WithStatusCode(HttpStatusCode.OK);
                }

                using (var session = Store.OpenSession())
                {
                    RavenQueryStatistics stats;
                    var message = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .Statistics(out stats)
                        .TransformWith<MessagesBodyTransformer, MessagesBodyTransformer.Result>()
                        .FirstOrDefault(f => f.MessageId == messageId);

                    if (message == null)
                    {
                        return new Response().WithStatusCode(HttpStatusCode.NotFound);
                    }

                    if (message.BodyNotStored)
                    {
                        return new Response().WithStatusCode(HttpStatusCode.NoContent);
                    }

                    if (message.Body == null)
                    {
                        return new Response().WithStatusCode(HttpStatusCode.NotFound);
                    }

                    var data = Encoding.UTF8.GetBytes(message.Body);

                    return new Response
                        {
                            Contents = s => s.Write(data, 0, data.Length)
                        }
                        .WithContentType(message.ContentType)
                        .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                        .WithHeader("Content-Length", message.BodySize.ToString())
                        .WithStatusCode(HttpStatusCode.OK);
                }
            };
        }
    }
}