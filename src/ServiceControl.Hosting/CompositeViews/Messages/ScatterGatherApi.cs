namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Autofac;
    using Infrastructure.Settings;
    using Infrastructure.WebApi;
    using Newtonsoft.Json;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

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


    // used to hoist the static jsonSerializer field across the generic instances
    public abstract class ScatterGatherApiBase
    {
        protected static JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializerSettings.CreateDefault());
    }

    public abstract class ScatterGatherApi<TIn, TOut> : ScatterGatherApiBase, IApi
        where TOut : class
    {
        protected ScatterGatherApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory)
        {
            Store = documentStore;
            Settings = settings;
            HttpClientFactory = httpClientFactory;
        }

        protected IDocumentStore Store { get; }
        protected Settings Settings { get; }
        protected Func<HttpClient> HttpClientFactory { get; }

        public async Task<HttpResponseMessage> Execute(ApiController controller, TIn input)
        {
            var remotes = Settings.RemoteInstances;
            var currentRequest = controller.Request;

            var instanceId = InstanceIdGenerator.FromApiUrl(Settings.ApiUrl);
            var tasks = new List<Task<QueryResult<TOut>>>(remotes.Length + 1)
            {
                LocalCall(currentRequest, input, instanceId)
            };
            foreach (var remote in remotes)
            {
                tasks.Add(RemoteCall(currentRequest, remote.ApiAsUri, InstanceIdGenerator.FromApiUrl(remote.ApiUri)));
            }

            var response = AggregateResults(currentRequest, await Task.WhenAll(tasks).ConfigureAwait(false));

            return Negotiator.FromQueryResult(currentRequest, response);
        }

        async Task<QueryResult<TOut>> LocalCall(HttpRequestMessage request, TIn input, string instanceId)
        {
            var result = await LocalQuery(request, input).ConfigureAwait(false);
            result.InstanceId = instanceId;
            return result;
        }

        protected abstract Task<QueryResult<TOut>> LocalQuery(HttpRequestMessage request, TIn input);

        public QueryResult<TOut> AggregateResults(HttpRequestMessage request, QueryResult<TOut>[] results)
        {
            var combinedResults = ProcessResults(request, results);

            return new QueryResult<TOut>(
                combinedResults,
                AggregateStats(results, combinedResults)
            );
        }

        protected abstract TOut ProcessResults(HttpRequestMessage request, QueryResult<TOut>[] results);

        protected virtual QueryStatsInfo AggregateStats(IEnumerable<QueryResult<TOut>> results, TOut processedResults)
        {
            var infos = results.Select(x => x.QueryStats).ToArray();

            return new QueryStatsInfo(
                string.Join("", infos.OrderBy(x => x.ETag).Select(x => x.ETag)),
                infos.Sum(x => x.TotalCount),
                infos.Max(x => x.HighestTotalCountOfAllTheInstances)
            );
        }

        async Task<QueryResult<TOut>> RemoteCall(HttpRequestMessage currentRequest, Uri remoteUri, string instanceId)
        {
            var fetched = await FetchAndParse(currentRequest, remoteUri).ConfigureAwait(false);
            fetched.InstanceId = instanceId;
            return fetched;
        }

        async Task<QueryResult<TOut>> FetchAndParse(HttpRequestMessage currentRequest, Uri remoteUri)
        {
            var instanceUri = currentRequest.RedirectToRemoteUri(remoteUri);
            var httpClient = HttpClientFactory();
            try
            {
                // Assuming SendAsync returns uncompressed response and the AutomaticDecompression is enabled on the http client.
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

    abstract class ScatterGatherApiNoInput<TOut> : ScatterGatherApi<NoInput, TOut>
        where TOut : class
    {
        protected ScatterGatherApiNoInput(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
        {
        }

        public Task<HttpResponseMessage> Execute(ApiController controller)
        {
            return Execute(controller, NoInput.Instance);
        }

        protected override Task<QueryResult<TOut>> LocalQuery(HttpRequestMessage request, NoInput input)
        {
            return LocalQuery(request);
        }

        protected abstract Task<QueryResult<TOut>> LocalQuery(HttpRequestMessage request);
    }
}