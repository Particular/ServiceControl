namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Autofac;
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

    public interface IApi
    {
    }

    public class ApisModule : Module
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

    public abstract class ScatterGatherApi<TIn, TOut> : IApi
        where TOut : class
    {
        static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
        static ILog logger = LogManager.GetLogger(typeof(ScatterGatherApi<TIn, TOut>));

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
                LocalQuery(currentRequest, input, instanceId)
            };
            foreach (var remote in remotes)
            {
                tasks.Add(FetchAndParse(currentRequest, remote.ApiUri, InstanceIdGenerator.FromApiUrl(remote.ApiUri)));
            }

            var response = AggregateResults(currentRequest, instanceId, await Task.WhenAll(tasks).ConfigureAwait(false));

            var negotiate = module.Negotiate;
            return negotiate.WithPartialQueryResult(response, currentRequest);
        }

        public abstract Task<QueryResult<TOut>> LocalQuery(Request request, TIn input, string instanceId);

        internal QueryResult<TOut> AggregateResults(Request request, string instanceId, QueryResult<TOut>[] results)
        {
            var combinedResults = ProcessResults(request, results);

            return new QueryResult<TOut>(
                combinedResults,
                instanceId,
                AggregateStats(results, combinedResults)
            );
        }

        protected abstract TOut ProcessResults(Request request, QueryResult<TOut>[] results);

        protected QueryResult<TOut> Results(TOut results, string instanceId, RavenQueryStatistics stats = null)
        {
            return stats != null
                ? new QueryResult<TOut>(results, instanceId, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults))
                : new QueryResult<TOut>(results, instanceId, QueryStatsInfo.Zero);
        }

        protected virtual QueryStatsInfo AggregateStats(IEnumerable<QueryResult<TOut>> results, TOut processedResults)
        {
            var infos = results.OrderBy(x => x.InstanceId, StringComparer.InvariantCultureIgnoreCase).Select(x => x.QueryStats).ToArray();

            return new QueryStatsInfo(
                string.Join("", infos.Select(x => x.ETag)),
                infos.Sum(x => x.TotalCount),
                infos.Max(x => x.HighestTotalCountOfAllTheInstances)
            );
        }


        async Task<QueryResult<TOut>> FetchAndParse(Request currentRequest, string remoteUri, string instanceId)
        {
            var instanceUri = new Uri($"{remoteUri}{currentRequest.Path}?{currentRequest.Url.Query}");
            var httpClient = HttpClientFactory();
            try
            {
                var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, instanceUri)).ConfigureAwait(false);
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

                return new QueryResult<TOut>(remoteResults, instanceId, new QueryStatsInfo(etag, totalCount, totalCount));
            }
        }
    }
}