namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Nancy;
    using Raven.Client.Documents;

    public class GetBodyByIdApi : RoutedApi<string>
    {
        public IDocumentStore Store { get; set; }
        IBodyStorage bodyStorage;

        public GetBodyByIdApi(IBodyStorage bodyStorage)
        {
            this.bodyStorage = bodyStorage;
        }

        protected override async Task<Response> LocalQuery(Request request, string input, string instanceId)
        {
            var messageId = input;
            messageId = messageId?.Replace("/", @"\");

            Action<Stream> contents;
            string contentType;
            long bodySize;

            //We want to continue using attachments for now
            var body = await bodyStorage.TryFetch(messageId).ConfigureAwait(false);

            if (!body.HasResult)
            {
                using (var session = Store.OpenAsyncSession())
                {
                    var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .Select(m => new Result
                        {
                            MessageId = m.MessageId,
                            Body = (string)m.MessageMetadata["Body"],
                            BodySize = (int)m.MessageMetadata["ContentLength"],
                            ContentType = (string)m.MessageMetadata["ContentType"],
                            BodyNotStored = (bool)m.MessageMetadata["BodyNotStored"]
                        })
                        .FirstOrDefaultAsync(f => f.MessageId == messageId)
                        .ConfigureAwait(false);

                    if (message != null && message.BodyNotStored)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    if (message == null && message.Body == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    var data = Encoding.UTF8.GetBytes(message.Body);
                    contents = stream => stream.Write(data, 0, data.Length);
                    contentType = message.ContentType;
                    bodySize = message.BodySize;
                }
            }
            else
            {
                contents = stream => body.Stream.CopyTo(stream);
                contentType = body.ContentType;
                bodySize = body.BodySize;
            }

            return new Response
                {
                    Contents = contents
                }
                .WithContentType(contentType)
                .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                .WithHeader("Content-Length", bodySize.ToString())
                .WithStatusCode(HttpStatusCode.OK);
        }

        class Result
        {
            public string MessageId { get; set; }
            public string Body { get; set; }
            public string ContentType { get; set; }
            public int BodySize { get; set; }
            public bool BodyNotStored { get; set; }
        }
    }
}