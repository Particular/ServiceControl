﻿namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Nancy;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.CompositeViews.Messages;

    public class GetBodyByIdApi : RoutedApi<string>
    {
        public IDocumentStore Store { get; set; }
        public IBodyStorage BodyStorage { get; set; }

        protected override async Task<Response> LocalQuery(Request request, string input, string instanceId)
        {
            string messageId = input;
            messageId = messageId?.Replace("/", @"\");
            Action<Stream> contents;
            string contentType;
            int bodySize;

            var result = BodyStorage.TryFetch(messageId);
            Etag currentEtag;

            if (result.HasResult)
            {
                using (var session = Store.OpenAsyncSession())
                {
                    RavenQueryStatistics stats;
                    var message = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                        .Statistics(out stats)
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