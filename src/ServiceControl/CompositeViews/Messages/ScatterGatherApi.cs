namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
        where TOut: class
    {
        static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializer.CreateDefault());
        static ILog logger = LogManager.GetLogger(typeof(ScatterGatherApi<TIn, TOut>));

        private Dictionary<string, string> instanceIdToApiUri;
        private Dictionary<string, string> apiUriToInstanceId;

        public IDocumentStore Store { get; set; }
        public Settings Settings { get; set; }
        public Func<HttpClient> HttpClientFactory { get; set; }
        public string CurrentInstanceId { get; set; }

        public Dictionary<string, string> InstanceIdToApiUri
        {
            get
            {
                if (instanceIdToApiUri == null)
                {
                    instanceIdToApiUri = Settings.RemoteInstances.ToDictionary(k => InstanceIdGenerator.FromApiUrl(k.ApiUri), v => v.ApiUri.ToLowerInvariant());
                    CurrentInstanceId = InstanceIdGenerator.FromApiUrl(Settings.ApiUrl);
                    instanceIdToApiUri.Add(CurrentInstanceId, Settings.ApiUrl.ToLowerInvariant());
                }

                return instanceIdToApiUri;
            }
        }

        public Dictionary<string, string> ApiUriToInstanceId
        {
            get
            {
                if (apiUriToInstanceId == null)
                {
                    apiUriToInstanceId = Settings.RemoteInstances.ToDictionary(k => k.ApiUri.ToLowerInvariant(), v => InstanceIdGenerator.FromApiUrl(v.ApiUri));
                    apiUriToInstanceId.Add(Settings.ApiUrl.ToLowerInvariant(), InstanceIdGenerator.FromApiUrl(Settings.ApiUrl));
                }

                return apiUriToInstanceId;
            }
        }

        public async Task<dynamic> Execute(BaseModule module, TIn input)
        {
            var remotes = Settings.RemoteInstances;
            var currentRequest = module.Request;
            var query = (DynamicDictionary)module.Request.Query;

            var tasks = new List<Task<QueryResult<TOut>>>(remotes.Length + 1);
            dynamic instanceId;
            if (query.TryGetValue("instance_id", out instanceId))
            {
                var id = (string) instanceId;
                if (id == CurrentInstanceId)
                {
                    tasks.Add(LocalQuery(currentRequest, input, ApiUriToInstanceId[Settings.ApiUrl.ToLowerInvariant()]));
                }
                else
                {
                    string remoteUri;
                    if (InstanceIdToApiUri.TryGetValue(id, out remoteUri))
                    {
                        tasks.Add(FetchAndParse(currentRequest, remoteUri));
                    }
                }
            }
            else
            {
                tasks.Add(LocalQuery(currentRequest, input, ApiUriToInstanceId[Settings.ApiUrl.ToLowerInvariant()]));
                foreach (var remote in remotes)
                {
                    tasks.Add(FetchAndParse(currentRequest, remote.ApiUri));
                }
            }

            var response = AggregateResults(currentRequest, await Task.WhenAll(tasks));

            var negotiate = module.Negotiate;
            return negotiate.WithPartialQueryResult(response, currentRequest);
        }

        public abstract Task<QueryResult<TOut>> LocalQuery(Request request, TIn input, string instanceId);

        public virtual QueryResult<TOut> AggregateResults(Request request, QueryResult<TOut>[] results)
        {
            var combinedResults = ProcessResults(request, results);

            return new QueryResult<TOut>(
                combinedResults,
                AggregateStats(results.Select(x => x.QueryStats).ToArray())
            );
        }

        protected abstract TOut ProcessResults(Request request, QueryResult<TOut>[] results);

        protected virtual QueryStatsInfo AggregateStats(QueryStatsInfo[] infos)
        {
            return new QueryStatsInfo(
                string.Join("", infos.Select(x => x.ETag)),
                infos.Max(x => x.LastModified),
                infos.Sum(x => x.TotalCount),
                infos.Max(x => x.HighestTotalCountOfAllTheInstances)
            );
        }

        protected QueryResult<TOut> Results(TOut results, RavenQueryStatistics stats = null)
        {
            return stats != null 
                ? new QueryResult<TOut>(results, new QueryStatsInfo(stats.IndexEtag, stats.IndexTimestamp, stats.TotalResults)) 
                : new QueryResult<TOut>(results, QueryStatsInfo.Zero);
        }


        async Task<QueryResult<TOut>> FetchAndParse(Request currentRequest, string remoteUri)
        {
            var instanceUri = new Uri($"{remoteUri}{currentRequest.Path}?{currentRequest.Url.Query}");
            var httpClient = HttpClientFactory();
            try
            {
                var rawResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, instanceUri)).ConfigureAwait(false);
                // special case - queried by conversation ID and nothing was found
                if (rawResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return QueryResult<TOut>.Empty;
                }

                return await ParseResult(rawResponse).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to query remote instance at {remoteUri}.", exception);
                return QueryResult<TOut>.Empty;
            }
        }

        static async Task<QueryResult<TOut>> ParseResult(HttpResponseMessage responseMessage)
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

                return new QueryResult<TOut>(remoteResults, new QueryStatsInfo(etag, lastModified, totalCount, totalCount));
            }
        }
    }
}