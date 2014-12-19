namespace ServiceControl.ProductionDebugging.RavenDB.Api
{
    using System;
    using System.IO;
    using System.Linq;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetBodyById : BaseModule
    {

        public GetBodyById()
        {
            Get["/messages/{id}/body"] = parameters =>
            {
                string messageId = parameters.id;
                Action<Stream> contents;
                string contentType;
                int bodySize;

                using (var session = Store.OpenSession())
                {
                    var message = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
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
                    var data = System.Text.Encoding.UTF8.GetBytes(message.Body);
                    contents = stream => stream.Write(data, 0, data.Length);
                    contentType = message.ContentType;
                    bodySize = message.BodySize;
                }

                return new Response { Contents = contents }
                    .WithContentType(contentType)
                    .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                    .WithHeader("Content-Length", bodySize.ToString())
                    .WithStatusCode(HttpStatusCode.OK);
            };
        }

    }

}