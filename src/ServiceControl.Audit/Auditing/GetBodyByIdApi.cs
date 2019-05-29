namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using BodyStorage;
    using MessagesView;
    using Nancy;
    using Raven.Abstractions.Data;
    using Raven.Client;

    class GetBodyByIdApi : IApi
    {
        public IDocumentStore Store { get; set; }
        public IBodyStorage BodyStorage { get; set; }

        public async Task<Response> Execute(string messageId)
        {
            messageId = messageId?.Replace("/", @"\");
            Action<Stream> contents;
            string contentType;
            int bodySize;

            //We want to continue using attachments for now
#pragma warning disable 618
            var result = await BodyStorage.TryFetch(messageId).ConfigureAwait(false);
#pragma warning restore 618
            Etag currentEtag;

            if (!result.HasResult)
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .Statistics(out var stats)
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
                contents = stream => result.Stream.CopyTo(stream);
                contentType = result.ContentType;
                bodySize = result.BodySize;
                currentEtag = result.Etag;
            }

            return new Response
                {
                    Contents = contents
                }
                .WithContentType(contentType)
                .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                .WithHeader("Content-Length", bodySize.ToString())
                .WithHeader("ETag", currentEtag)
                .WithStatusCode(HttpStatusCode.OK);
        }
    }
}