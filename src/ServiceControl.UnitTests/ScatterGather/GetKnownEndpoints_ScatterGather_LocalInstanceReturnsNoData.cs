namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    class GetKnownEndpoints_ScatterGather_LocalInstanceReturnsNoData : GetKnownEndpoints_ScatterGatherTest
    {
        protected override IEnumerable<QueryResult<List<KnownEndpointsView>>> GetData()
        {
            yield return LocalPage(50);
            yield return RemotePage(1);
        }

        [Test]
        public void ResultsMatchRemote()
        {
            var remoteResults = RemotePage(1);
            var returnedEndpointIds = Results.Results.Select(x => x.Id).ToList();

            foreach (var remoteResult in remoteResults.Results)
            {
                Assert.Contains(remoteResult.Id, returnedEndpointIds, "Remote result missing in returned Results");
            }
        }
    }
}