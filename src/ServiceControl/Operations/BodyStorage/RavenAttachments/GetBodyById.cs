namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.Linq;
    using CompositeViews.Messages;
    using System.Text;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetBodyById : BaseModule
    {
        public IMessageBodyStore MessageBodyStorage { get; set; }

        public GetBodyById()
        {
            Get["/messages/{id*}/body_v2"] = parameters =>
            {
                string messageId = parameters.id;
                messageId = messageId?.Replace("/", @"\");

                return GetBodyFromNewBodyStore(messageId)
                    ?? GetBodyFromMetadataStore(messageId);
            };

            Get["/messages/{id*}/body"] = parameters =>
            {
                string messageId = parameters.id;
                messageId = messageId?.Replace("/", @"\");

                return GetBodyFromNewBodyStore(messageId)
                    ?? GetBodyFromMetadataStore(messageId);
            };
        }

        Response GetBodyFromNewBodyStore(string messageId)
        {
            byte[] messageBody;
            MessageBodyMetadata metadata;

            var found = MessageBodyStorage.TryGet(BodyStorageTags.Audit, messageId, out messageBody, out metadata);

            if (!found)
            {
                found = MessageBodyStorage.TryGet(BodyStorageTags.ErrorPersistent, messageId, out messageBody, out metadata);
            }

            if (!found)
            {
                found = MessageBodyStorage.TryGet(BodyStorageTags.ErrorTransient, messageId, out messageBody, out metadata);
            }

            if (found)
            {
                return new Response
                {
                    Contents = stream => stream.Write(messageBody, 0, messageBody.Length)
                }
                    .WithContentType(metadata.ContentType)
                    .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                    .WithHeader("Content-Length", metadata.Size.ToString())
                    .WithStatusCode(HttpStatusCode.OK);
            }
            return null;
        }

        Response GetBodyFromMetadataStore(string messageId)
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
        }
    }
}