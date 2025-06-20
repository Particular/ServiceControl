namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using Persistence.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class MessageView_ScatterGatherTest
    {
        [SetUp]
        public void SetUp()
        {
            var api = new TestApi(null, null, null, NullLogger<TestApi>.Instance);

            Results = api.AggregateResults(new ScatterGatherApiMessageViewContext(new PagingInfo(), new SortInfo()), GetData());
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
                new QueryStatsInfo(etag, allResults.Count, isStale: false))
            {
                InstanceId = instanceId
            };
        }

        protected IEnumerable<MessagesView> LocalData()
        {
            for (var i = 0; i < 200; i++)
            {
                yield return new MessagesView { MessageId = Guid.NewGuid().ToString() };
            }
        }

        protected IEnumerable<MessagesView> RemoteData()
        {
            for (var i = 0; i < 55; i++)
            {
                yield return new MessagesView { MessageId = Guid.NewGuid().ToString() };
            }
        }

        protected QueryResult<IList<MessagesView>> Results;
        protected const string LocalInstanceID = nameof(LocalInstanceID);
        protected const string LocalETag = nameof(LocalETag);
        protected const string RemoteInstanceId = nameof(RemoteInstanceId);
        protected const string RemoteETag = nameof(RemoteETag);
        protected const int PageSize = 50;

        class TestApi : ScatterGatherApiMessageView<object, ScatterGatherApiMessageViewContext>
        {
            public TestApi(object dataStore, Settings settings, IHttpClientFactory httpClientFactory, ILogger<TestApi> logger)
                : base(dataStore, settings, httpClientFactory, logger)
            {
            }

            protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewContext input) => throw new NotImplementedException();
        }
    }
}