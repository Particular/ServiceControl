namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;

    abstract class MessageView_ScatterGatherTest
    {
        protected virtual string QueryString => string.Empty;

        [SetUp]
        public void SetUp()
        {
            var api = new TestApi(null, null, null);

            var request = new HttpRequestMessage(new HttpMethod("GET"), $"http://doesnt/really/matter?{QueryString}");

            Results = api.AggregateResults(request, GetData());
        }

        protected abstract QueryResult<IList<MessagesView>>[] GetData();

        protected QueryResult<IList<MessagesView>> LocalPage(int page, int pageSize = PageSize)
            => GetPage(LocalData(), LocalInstanceID, LocalETag, page, pageSize);

        protected QueryResult<IList<MessagesView>> RemotePage(int page, int pageSize = PageSize)
            => GetPage(RemoteData(), RemoteInstanceId, RemoteETag, page, pageSize);

        QueryResult<IList<MessagesView>> GetPage(IEnumerable<MessagesView> source, string instanceId, string etag, int page, int pageSize)
        {
            var allResults = source.OrderBy(_ => Guid.NewGuid()).ToList();
            var pageOfResults = allResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new QueryResult<IList<MessagesView>>(
                pageOfResults,
                new QueryStatsInfo(etag, allResults.Count))
            {
                InstanceId = instanceId
            };
        }

        protected IEnumerable<MessagesView> LocalData()
        {
            for (var i = 0; i < 200; i++)
            {
                yield return new MessagesView();
            }
        }

        protected IEnumerable<MessagesView> RemoteData()
        {
            for (var i = 0; i < 55; i++)
            {
                yield return new MessagesView();
            }
        }

        protected QueryResult<IList<MessagesView>> Results;
        protected const string LocalInstanceID = nameof(LocalInstanceID);
        protected const string LocalETag = nameof(LocalETag);
        protected const string RemoteInstanceId = nameof(RemoteInstanceId);
        protected const string RemoteETag = nameof(RemoteETag);
        protected const int PageSize = 50;

        class TestApi : ScatterGatherApiMessageView<NoInput>
        {
            public TestApi(IDocumentStore documentStore, Settings settings, Func<HttpClient> httpClientFactory) : base(documentStore, settings, httpClientFactory)
            {
            }

            protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(HttpRequestMessage request, NoInput input)
            {
                throw new NotImplementedException();
            }
        }
    }
}