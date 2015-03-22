namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetBodyById : BaseModule
    {

        public GetBodyById()
        {
            Get["/messagebodies/{id*}"] = parameters =>
            {
                string messageId = parameters.id;
                if (messageId != null)
                {
                    messageId = messageId.Replace("/", @"\");
                }
                var attachment = Store.DatabaseCommands.GetAttachment("messagebodies/" + messageId);
                if (attachment == null)
                {
                    return HttpStatusCode.NotFound;
                }

                Action<Stream> contents = stream => attachment.Data().CopyTo(stream);
                var contentType = attachment.Metadata["ContentType"].Value<string>();
                var bodySize = attachment.Metadata["ContentLength"].Value<int>();

                return new Response { Contents = contents }
                    .WithContentType(contentType)
                    .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R")) 
                    .WithHeader("Content-Length", bodySize.ToString())
                    .WithStatusCode(HttpStatusCode.OK);
            };
        }

    }

}