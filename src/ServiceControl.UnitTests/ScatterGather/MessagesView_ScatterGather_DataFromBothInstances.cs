namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence.Infrastructure;

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
            Assert.That(Results.Results, Is.Not.Empty, "There should be results returned");
        }

        [Test]
        public void ResultingETagIsDifferentFromBothInstanceSpecificETags()
        {
            var resultingEtag = Results.QueryStats.ETag;

            Assert.That(resultingEtag, Is.Not.EqualTo(LocalETag), "Resulting etag should not equal local etag");
            Assert.That(resultingEtag, Is.Not.EqualTo(RemoteETag), "Resulting etag should not equal remote etag");
        }

        [Test]
        public void TotalCountHeaderMatchesSumOfAllResults()
        {
            var sumOfAllResults = LocalData().Count() + RemoteData().Count();

            Assert.That(Results.QueryStats.TotalCount, Is.EqualTo(sumOfAllResults));
        }

        [Test]
        public void HighestCountIsAccurate()
        {
            var highestInstanceCount = Math.Max(LocalData().Count(), RemoteData().Count());

            Assert.That(Results.QueryStats.HighestTotalCountOfAllTheInstances, Is.EqualTo(highestInstanceCount));
        }
    }
}