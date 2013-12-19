namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetBodyById : BaseModule
    {

        public GetBodyById()
        {
            Get["/messages/{id}/body"] = parameters =>
            {
                string messageId = parameters.id;

                var attachment = Store.DatabaseCommands.GetAttachment("messagebodies/" + messageId);


                if (attachment == null)
                {
                    return HttpStatusCode.NotFound;
                }

                return new Response{ Contents = stream => attachment.Data().CopyTo(stream)}
                    .WithContentType(attachment.Metadata["ContentType"].Value<string>())
                    .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'")) // cache "forever"
                    .WithHeader("Content-Length", attachment.Metadata["ContentLength"].Value<string>()); // cache "forever"
            };
        }

    }

}