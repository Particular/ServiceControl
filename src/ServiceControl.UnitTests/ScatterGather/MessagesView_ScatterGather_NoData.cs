namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    public class MessagesView_ScatterGather_NoData : MessageView_ScatterGatherTest
    {
        protected override IEnumerable<QueryResult<List<MessagesView>>> GetData()
        {
            yield return QueryResult<List<MessagesView>>.Empty();
            yield return QueryResult<List<MessagesView>>.Empty();
        }

        [Test]
        public void NoResults()
        {
            Assert.AreEqual(0, Results.Results.Count, "There should be no Results");
        }
    }
}