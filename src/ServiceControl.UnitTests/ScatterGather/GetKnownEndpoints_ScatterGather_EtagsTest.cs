namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using Nancy;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    class GetKnownEndpoints_ScatterGather_EtagsTest
    {
        [Test]
        public void ResultOrderDoesNotEffectEtag()
        {
            var api = new GetKnownEndpointsApi();

            var request = new Request("GET", new Url("http://localhost/api/endpoints/known"));

            var localPage = new QueryResult<List<KnownEndpointsView>>(
                new List<KnownEndpointsView>(0),
                new QueryStatsInfo(LocalETag, 0))
            {
                InstanceId = LocalInstanceID
            };

            var remotePage = new QueryResult<List<KnownEndpointsView>>(
                new List<KnownEndpointsView>(0),
                new QueryStatsInfo(RemoteETag, 0))
            {
                InstanceId = RemoteInstanceId
            };

            var localFirst = api.AggregateResults(request, new[]
            {
                localPage,
                remotePage
            });
            var remoteFirst = api.AggregateResults(request, new[]
            {
                remotePage,
                localPage
            });

            Assert.AreEqual(localFirst.QueryStats.ETag, remoteFirst.QueryStats.ETag, "etag should not depend on result ordering");
        }

        protected const string LocalInstanceID = nameof(LocalInstanceID);
        protected const string LocalETag = nameof(LocalETag);
        protected const string RemoteInstanceId = nameof(RemoteInstanceId);
        protected const string RemoteETag = nameof(RemoteETag);
    }
}