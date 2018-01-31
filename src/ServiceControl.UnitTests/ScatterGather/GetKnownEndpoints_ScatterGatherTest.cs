namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using System.Linq;
    using Nancy;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure;

    abstract class GetKnownEndpoints_ScatterGatherTest
    {
        protected const string LocalInstanceID = nameof(LocalInstanceID);
        protected const string LocalETag = nameof(LocalETag);
        protected const string RemoteInstanceId = nameof(RemoteInstanceId);
        protected const string RemoteETag = nameof(RemoteETag);
        protected const string Web1 = "WEB-01";
        protected const string Web2 = "WEB-02";
        protected const string App1 = "APP-01";
        protected const string App2 = "APP-02";
        protected const string FrontEnd = nameof(FrontEnd);
        protected const string Sales = nameof(Sales);
        protected const string Shipping = nameof(Shipping);
        protected const int PageSize = 50;

        protected QueryResult<List<KnownEndpointsView>> Results;

        [SetUp]
        public void SetUp()
        {
            var api = new GetKnownEndpointsApi();

            var request = new Request("GET", new Url("http://localhost/api/endpoints/known"));

            Results = api.AggregateResults(request, LocalInstanceID, GetData().ToArray());

        }

        protected abstract IEnumerable<QueryResult<List<KnownEndpointsView>>> GetData();

        [Test]
        public void StatsMatchReturnedResults()
        {
            Assert.AreEqual(Results.Results.Count, Results.QueryStats.TotalCount, "Stats count should include all of the Results");
        }

        protected QueryResult<List<KnownEndpointsView>> LocalPage(int page, int pageSize = PageSize)
        {
            var result = LocalData().Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new QueryResult<List<KnownEndpointsView>>(result, LocalInstanceID, new QueryStatsInfo(LocalETag, result.Count));
        }

        protected QueryResult<List<KnownEndpointsView>> RemotePage(int page, int pageSize = PageSize)
        {
            var result = RemoteData().Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new QueryResult<List<KnownEndpointsView>>(result, RemoteInstanceId, new QueryStatsInfo(RemoteETag, result.Count));
        }

        private IEnumerable<KnownEndpointsView> LocalData()
        {
            yield return Endpoint(Web1, FrontEnd);
            yield return Endpoint(Web2, FrontEnd);
            yield return Endpoint(App1, Sales);
        }

        private IEnumerable<KnownEndpointsView> RemoteData()
        {
            yield return Endpoint(App1, Sales);
            yield return Endpoint(App1, Shipping);
            yield return Endpoint(App2, Shipping);
        }

        private KnownEndpointsView Endpoint(string host, string endpoint)
        {
            return new KnownEndpointsView
            {
                Id = DeterministicGuid.MakeId(host, endpoint),
                HostDisplayName = host,
                EndpointDetails = new EndpointDetails
                {
                    Host = host,
                    HostId = DeterministicGuid.MakeId(host, endpoint),
                    Name = endpoint
                }
            };
        }
    }
}