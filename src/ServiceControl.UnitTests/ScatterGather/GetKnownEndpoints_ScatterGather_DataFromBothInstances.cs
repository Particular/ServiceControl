namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    class GetKnownEndpoints_ScatterGather_DataFromBothInstances : GetKnownEndpoints_ScatterGatherTest
    {
        protected override IEnumerable<QueryResult<List<KnownEndpointsView>>> GetData()
        {
            yield return LocalPage(1);
            yield return RemotePage(1);
        }

        [Test]
        public void ResultsContainsNoDuplicates()
        {
            var duplicates = Results.Results.GroupBy(x => x.Id).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();

            Assert.IsEmpty(duplicates, "There should be no duplicate endpoints in the Results");
        }

        [Test]
        public void ResultingETagIsDifferentFromBothInstanceSpecificETags()
        {
            var resultingEtag = Results.QueryStats.ETag;

            Assert.AreNotEqual(LocalETag, resultingEtag, "Resulting etag should not equal local etag");
            Assert.AreNotEqual(RemoteETag, resultingEtag, "Resulting etag should not equal remote etag");
        }

        [Test]
        public void AllResultsAreReturned()
        {
            Assert.AreEqual(5, Results.Results.Count, "There should be 5 Results");
            Assert.AreEqual(5, Results.QueryStats.TotalCount, "The headers should report 5 Results");
        }
    }
}