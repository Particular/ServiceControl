namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    class GetKnownEndpoints_ScatterGather_NoData : GetKnownEndpoints_ScatterGatherTest
    {
        protected override IEnumerable<QueryResult<List<KnownEndpointsView>>> GetData()
        {
            yield return QueryResult<List<KnownEndpointsView>>.Empty();
            yield return QueryResult<List<KnownEndpointsView>>.Empty();
        }

        [Test]
        public void NoResults()
        {
            Assert.AreEqual(0, Results.Results.Count, "There should be no Results");
        }
    }
}