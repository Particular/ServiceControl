namespace ServiceControl.Operations.BodyStorage
{
    using System;
    using MessageAuditing;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetEmbeddedBodyById : BaseModule
    {

        public GetEmbeddedBodyById()
        {
            Get["/messages/{id}/embeddedbody"] = parameters =>
            {
                string messageId = parameters.id;


                using (var session = Store.OpenSession())
                {
                    var message = session.Load<ProcessedMessage>(Guid.Parse(messageId));

                    if (message == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    object body;

                    if (!message.MessageMetadata.TryGetValue("Body", out body))
                    {
                        return HttpStatusCode.NotFound;
                    }

                    var bodyBuffer = System.Text.Encoding.UTF8.GetBytes((string)body);

                    return new Response { Contents = stream => stream.Write(bodyBuffer, 0, bodyBuffer.Length) }
                        .WithContentType(message.MessageMetadata["ContentType"].ToString())
                        .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'")) // cache "forever"
                        .WithHeader("Content-Length", message.MessageMetadata["BodySize"].ToString());

                }
            };
        }

    }
}