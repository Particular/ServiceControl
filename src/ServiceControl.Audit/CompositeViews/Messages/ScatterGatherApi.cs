namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Autofac;
    using Infrastructure.Settings;
    using Nancy;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.Settings;
    using HttpStatusCode = System.Net.HttpStatusCode;

    interface IApi
    {
    }

    class ApisModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IApi>()
                .AsSelf()
                .AsImplementedInterfaces()
                .PropertiesAutowired();
        }
    }

    // used to hoist the static jsonSerializer field across the generic instances
    abstract class ScatterGatherApiBase
    {
        protected static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
    }

    abstract class ScatterGatherApi<TIn, TOut> : ScatterGatherApiBase, IApi
        where TOut : class
    {
        public IDocumentStore Store { get; set; }
        public Settings Settings { get; set; }
        public Func<HttpClient> HttpClientFactory { get; set; }

        public async Task<dynamic> Execute(BaseModule module, TIn input)
        {
            var remotes = Settings.RemoteInstances;
            var currentRequest = module.Request;

            var instanceId = InstanceIdGenerator.FromApiUrl(Settings.ApiUrl);
            var tasks = new List<Task<QueryResult<TOut>>>(remotes.Length + 1)
            {
                LocalCall(currentRequest, input, instanceId)
            };
            foreach (var remote in remotes)
            {
                tasks.Add(RemoteCall(currentRequest, remote.ApiUri, InstanceIdGenerator.FromApiUrl(remote.ApiUri)));
            }

            var response = AggregateResults(currentRequest, await Task.WhenAll(tasks).ConfigureAwait(false));

            var negotiate = module.Negotiate;
            return negotiate.WithQueryResult(response, currentRequest);
        }

        async Task<QueryResult<TOut>> LocalCall(Request request, TIn input, string instanceId)
        {
            var result = await LocalQuery(request, input).ConfigureAwait(false);
            result.InstanceId = instanceId;
            return result;
        }

        public abstract Task<QueryResult<TOut>> LocalQuery(Request request, TIn input);

        internal QueryResult<TOut> AggregateResults(Request request, QueryResult<TOut>[] results)
        {
            var combinedResults = ProcessResults(request, results);

            return new QueryResult<TOut>(
                combinedResults,
                AggregateStats(results, combinedResults)
            );
        }

        protected abstract TOut ProcessResults(Request request, QueryResult<TOut>[] results);

        protected virtual QueryStatsInfo AggregateStats(IEnumerable<QueryResult<TOut>> results, TOut processedResults)
        {
            var infos = results.Select(x => x.QueryStats).ToArray();

            return new QueryStatsInfo(
                string.Join("", infos.OrderBy(x => x.ETag).Select(x => x.ETag)),
                infos.Sum(x => x.TotalCount),
                infos.Max(x => x.HighestTotalCountOfAllTheInstances)
            );
        }

        async Task<QueryResult<TOut>> RemoteCall(Request currentRequest, string remoteUri, string instanceId)
        {
            var fetched = await FetchAndParse(currentRequest, remoteUri, instanceId).ConfigureAwait(false);
            fetched.InstanceId = instanceId;
            return fetched;
        }

        async Task<QueryResult<TOut>> FetchAndParse(Request currentRequest, string remoteUri, string instanceId)
        {
            var instanceUri = currentRequest.RedirectToRemoteUri(remoteUri);
            var httpClient = HttpClientFactory();
            try
            {
                var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, instanceUri)).ConfigureAwait(false);
                // special case - queried by conversation ID and nothing was found
                if (rawResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return QueryResult<TOut>.Empty();
                }

                return await ParseResult(rawResponse).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to query remote instance at {remoteUri}.", exception);
                return QueryResult<TOut>.Empty();
            }
        }

        static async Task<QueryResult<TOut>> ParseResult(HttpResponseMessage responseMessage)
        {
            using (var responseStream = await responseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var jsonReader = new JsonTextReader(new StreamReader(responseStream)))
            {
                var remoteResults = jsonSerializer.Deserialize<TOut>(jsonReader);

                var totalCount = 0;
                if (responseMessage.Headers.TryGetValues("Total-Count", out var totalCounts))
                {
                    totalCount = int.Parse(totalCounts.ElementAt(0));
                }

                string etag = null;
                if (responseMessage.Headers.TryGetValues("ETag", out var etags))
                {
                    etag = etags.ElementAt(0);
                }

                return new QueryResult<TOut>(remoteResults, new QueryStatsInfo(etag, totalCount));
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(ScatterGatherApi<TIn, TOut>));
    }
}