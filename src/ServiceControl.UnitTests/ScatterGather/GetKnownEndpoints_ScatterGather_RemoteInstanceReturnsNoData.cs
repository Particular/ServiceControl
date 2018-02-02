namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    class GetKnownEndpoints_ScatterGather_RemoteInstanceReturnsNoData : GetKnownEndpoints_ScatterGatherTest
    {
        protected override IEnumerable<QueryResult<List<KnownEndpointsView>>> GetData()
        {
            yield return LocalPage(1);
            yield return RemotePage(50);
        }

        [Test]
        public void ResultsMatchLocal()
        {
            var localResults = LocalPage(1);
            var returnedEndpointIds = Results.Results.Select(x => x.Id).ToList();

            Assert.IsNotEmpty(localResults.Results);
            foreach (var localResult in localResults.Results)
            {
                Assert.Contains(localResult.Id, returnedEndpointIds, "Local result missing in returned Results");
            }
        }
    }
}