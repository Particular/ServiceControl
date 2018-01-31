namespace ServiceControl.Operations.BodyStorage.Api
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using CompositeViews.Messages;
    using System.Text;
    using Nancy;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Infrastructure.Settings;

    public class GetBodyById : BaseModule
    {
        static ILog logger = LogManager.GetLogger<GetBodyById>();
        public Func<HttpClient> HttpClientFactory { get; set; }

        public GetBodyById()
        {
            Get["/messages/{id*}/body", true] = async (parameters, token) =>
            {
                var query = (DynamicDictionary)Request.Query;

                var localInstanceId = InstanceIdGenerator.FromApiUrl(Settings.ApiUrl);

                dynamic instanceId;
                if (query.TryGetValue("instance_id", out instanceId) && instanceId != localInstanceId)
                {
                    var remoteUri = InstanceIdGenerator.ToApiUrl(instanceId);
                    var instanceUri = new Uri($"{remoteUri}{Request.Path}?{Request.Url.Query}");

                    var httpClient = HttpClientFactory();
                    try
                    {
                        var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, instanceUri)).ConfigureAwait(false);

                        if (rawResponse.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            return new Response
                                {
                                    Contents = stream => rawResponse.Content.CopyToAsync(stream).GetAwaiter().GetResult()
                                }
                                .WithContentType(rawResponse.Content.Headers.ContentType.ToString())
                                .WithHeader("Expires", DateTime.UtcNow.AddYears(1).ToUniversalTime().ToString("R"))
                                .WithHeader("Content-Length", rawResponse.Content.Headers.ContentLength.ToString())
                                .WithHeader("ETag", rawResponse.Headers.GetValues("ETag").SingleOrDefault())
                                .WithStatusCode(HttpStatusCode.OK);
                        }

                        logger.Info($"Remote instance '{remoteUri}' returned status code '{rawResponse.StatusCode}', forwarding to requestor.");
                        return rawResponse.StatusCode;
                    }
                    catch (Exception exception)
                    {
                        logger.Warn($"Failed to query remote instance at {remoteUri}.", exception);

                        return HttpStatusCode.InternalServerError;
                    }
                }

                string messageId = parameters.id;
                messageId = messageId?.Replace("/", @"\");
                Action<Stream> contents;
                string contentType;
                int bodySize;
                var attachment = await Store.AsyncDatabaseCommands.GetAttachmentAsync("messagebodies/" + messageId).ConfigureAwait(false);
                Etag currentEtag;

                if (attachment == null)
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
                    contents = stream => attachment.Data().CopyTo(stream);
                    contentType = attachment.Metadata["ContentType"].Value<string>();
                    bodySize = attachment.Metadata["ContentLength"].Value<int>();
                    currentEtag = attachment.Etag;
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
            };
        }

    }

}