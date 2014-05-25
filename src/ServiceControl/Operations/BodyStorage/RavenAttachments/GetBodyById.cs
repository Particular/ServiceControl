namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using System.Linq;
    using CompositeViews.Messages;
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
                var attachment = Store.DatabaseCommands.GetAttachment("messagebodies/" + messageId);

                if (attachment == null)
                {
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
                }
                else
                {
                    contents = stream => attachment.Data().CopyTo(stream);
                    contentType = attachment.Metadata["ContentType"].Value<string>();
                    bodySize = attachment.Metadata["ContentLength"].Value<int>();
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