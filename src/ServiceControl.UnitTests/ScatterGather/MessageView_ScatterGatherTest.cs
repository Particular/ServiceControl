namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence.Infrastructure;

    abstract class MessageView_ScatterGatherTest
    {
        protected virtual string QueryString => string.Empty;

        [SetUp]
        public void SetUp()
        {
            var api = new TestApi(null, null, null, null);

            // var request = new HttpRequestMessage(new HttpMethod("GET"), $"http://doesnt/really/matter?{QueryString}");

            // TODO Fix this because it will throw NullRef
            Results = api.AggregateResults(new ScatterGatherApiMessageViewContext(null, null), GetData());
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
            public TestApi(object dataStore, Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
                : base(dataStore, settings, httpClientFactory, httpContextAccessor)
            {
            }

            protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewContext input) => throw new NotImplementedException();
        }
    }
}