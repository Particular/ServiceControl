namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Nancy;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Settings;
    using HttpStatusCode = System.Net.HttpStatusCode;

    public abstract class RoutedApi<TIn, TOut> : IApi
        where TOut : class
    {
        static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
        static ILog logger = LogManager.GetLogger(typeof(RoutedApi<TIn, TOut>));

        public IDocumentStore Store { get; set; }
        public Settings Settings { get; set; }
        public Func<HttpClient> HttpClientFactory { get; set; }

        public async Task<dynamic> Execute(BaseModule module, TIn input)
        {
            var currentRequest = module.Request;
            QueryResult<TOut> response;

            var instanceId = GetInstance(currentRequest, input);

            var localInstanceId = InstanceIdGenerator.FromApiUrl(Settings.ApiUrl);

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                if (instanceId == localInstanceId)
                {
                    response = await LocalQuery(currentRequest, input, localInstanceId);
                }
                else
                {
                    response = await RemoteCall(currentRequest, instanceId);
                }
            }
            else
            {
                response = await LocalQuery(currentRequest, input, localInstanceId);
            }

            var negotiate = module.Negotiate;
            return negotiate.WithPartialQueryResult(response, currentRequest);
        }

        public virtual string GetInstance(Request currentRequest, TIn input)
        {
            return (string)currentRequest.Query.instance_id;
        }

        public abstract Task<QueryResult<TOut>> LocalQuery(Request request, TIn input, string instanceId);

        private async Task<QueryResult<TOut>> RemoteCall(Request currentRequest, string instanceId)
        {
            var remoteUri = InstanceIdGenerator.ToApiUrl(instanceId);

            var instanceUri = new Uri($"{remoteUri}{currentRequest.Path}?{currentRequest.Url.Query}");
            var httpClient = HttpClientFactory();
            try
            {
                var method = (HttpMethod)Enum.Parse(typeof(HttpMethod), currentRequest.Method, ignoreCase: true);
                // TODO: Should probably pass body through if it's available for POST requests
                var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(method, instanceUri)).ConfigureAwait(false);
                // special case - queried by conversation ID and nothing was found
                if (rawResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return QueryResult<TOut>.Empty(instanceId);
                }

                return await ParseResult(rawResponse, instanceId).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to query remote instance at {remoteUri}.", exception);
                return QueryResult<TOut>.Empty(instanceId);
            }

        }

        static async Task<QueryResult<TOut>> ParseResult(HttpResponseMessage responseMessage, string instanceId)
        {
            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var jsonReader = new JsonTextReader(new StreamReader(responseStream)))
            {
                var remoteResults = jsonSerializer.Deserialize<TOut>(jsonReader);

                IEnumerable<string> totalCounts;
                var totalCount = 0;
                if (responseMessage.Headers.TryGetValues("Total-Count", out totalCounts))
                {
                    totalCount = int.Parse(totalCounts.ElementAt(0));
                }

                IEnumerable<string> etags;
                string etag = null;
                if (responseMessage.Headers.TryGetValues("ETag", out etags))
                {
                    etag = etags.ElementAt(0);
                }

                IEnumerable<string> lastModifiedValues;
                var lastModified = DateTime.UtcNow;
                if (responseMessage.Headers.TryGetValues("Last-Modified", out lastModifiedValues))
                {
                    lastModified = DateTime.ParseExact(lastModifiedValues.ElementAt(0), "R", CultureInfo.InvariantCulture);
                }

                return new QueryResult<TOut>(remoteResults, instanceId, new QueryStatsInfo(etag, lastModified, totalCount, totalCount));
            }
        }


    }
}