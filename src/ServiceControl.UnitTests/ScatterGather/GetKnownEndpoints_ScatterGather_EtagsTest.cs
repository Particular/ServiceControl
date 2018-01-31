namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using Nancy;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    class GetKnownEndpoints_ScatterGather_EtagsTest
    {
        protected const string LocalInstanceID = nameof(LocalInstanceID);
        protected const string LocalETag = nameof(LocalETag);
        protected const string RemoteInstanceId = nameof(RemoteInstanceId);
        protected const string RemoteETag = nameof(RemoteETag);

        [Test]
        public void ResultOrderDoesNotEffectEtag()
        {
            var api = new GetKnownEndpointsApi();

            var request = new Request("GET", new Url("http://localhost/api/endpoints/known"));

            var localPage = new QueryResult<List<KnownEndpointsView>>(
                new List<KnownEndpointsView>(0), 
                LocalInstanceID,
                new QueryStatsInfo(LocalETag, 0));

            var remotePage = new QueryResult<List<KnownEndpointsView>>(
                new List<KnownEndpointsView>(0),
                RemoteInstanceId,
                new QueryStatsInfo(RemoteETag, 0));

            var localFirst = api.AggregateResults(request, LocalInstanceID, new[]
            {
                localPage,
                remotePage
            });
            var remoteFirst = api.AggregateResults(request, LocalInstanceID, new[]
            {
                remotePage,
                localPage
            });

            Assert.AreEqual(localFirst.QueryStats.ETag, remoteFirst.QueryStats.ETag, "etag should not depend on result ordering");

        }
    }
}