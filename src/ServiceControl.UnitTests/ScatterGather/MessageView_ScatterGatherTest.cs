﻿namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Nancy;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    abstract class MessageView_ScatterGatherTest
    {
        protected virtual string QueryString => string.Empty;

        [SetUp]
        public void SetUp()
        {
            var api = new TestApi();

            var request = new Request("GET", new Url($"http://doesnt/really/matter?{QueryString}"));

            Results = api.AggregateResults(request, GetData().ToArray());
        }

        protected abstract IEnumerable<QueryResult<List<MessagesView>>> GetData();

        protected QueryResult<List<MessagesView>> LocalPage(int page, int pageSize = PageSize)
            => GetPage(LocalData(), LocalInstanceID, LocalETag, page, pageSize);

        protected QueryResult<List<MessagesView>> RemotePage(int page, int pageSize = PageSize)
            => GetPage(RemoteData(), RemoteInstanceId, RemoteETag, page, pageSize);

        private QueryResult<List<MessagesView>> GetPage(IEnumerable<MessagesView> source, string instanceId, string etag, int page, int pageSize)
        {
            var allResults = source.OrderBy(_ => Guid.NewGuid()).ToList();
            var pageOfResults = allResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new QueryResult<List<MessagesView>>(
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

        protected QueryResult<List<MessagesView>> Results;
        protected const string LocalInstanceID = nameof(LocalInstanceID);
        protected const string LocalETag = nameof(LocalETag);
        protected const string RemoteInstanceId = nameof(RemoteInstanceId);
        protected const string RemoteETag = nameof(RemoteETag);
        protected const int PageSize = 50;

        class TestApi : ScatterGatherApiMessageView<NoInput>
        {
            public override Task<QueryResult<List<MessagesView>>> LocalQuery(Request request, NoInput input)
            {
                throw new NotImplementedException();
            }
        }
    }
}