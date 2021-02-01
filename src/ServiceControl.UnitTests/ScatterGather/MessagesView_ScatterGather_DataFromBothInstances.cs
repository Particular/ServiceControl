namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    class MessagesView_ScatterGather_DataFromBothInstances : MessageView_ScatterGatherTest
    {
        protected override QueryResult<IList<MessagesView>>[] GetData()
        {
            return new[] { LocalPage(1), RemotePage(1) };
        }

        [Test]
        public void HasResults()
        {
            Assert.IsNotEmpty(Results.Results, "There should be results returned");
        }

        [Test]
        public void ResultingETagIsDifferentFromBothInstanceSpecificETags()
        {
            var resultingEtag = Results.QueryStats.ETag;

            Assert.AreNotEqual(LocalETag, resultingEtag, "Resulting etag should not equal local etag");
            Assert.AreNotEqual(RemoteETag, resultingEtag, "Resulting etag should not equal remote etag");
        }

        [Test]
        public void TotalCountHeaderMatchesSumOfAllResults()
        {
            var sumOfAllResults = LocalData().Count() + RemoteData().Count();

            Assert.AreEqual(sumOfAllResults, Results.QueryStats.TotalCount);
        }

        [Test]
        public void HighestCountIsAccurate()
        {
            var highestInstanceCount = Math.Max(LocalData().Count(), RemoteData().Count());

            Assert.AreEqual(highestInstanceCount, Results.QueryStats.HighestTotalCountOfAllTheInstances);
        }
    }
}