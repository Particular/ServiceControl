namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    class MessagesView_ScatterGather_NoData : MessageView_ScatterGatherTest
    {
        protected override QueryResult<IList<MessagesView>>[] GetData()
        {
            return new[] {QueryResult<IList<MessagesView>>.Empty(), QueryResult<IList<MessagesView>>.Empty()};
        }

        [Test]
        public void NoResults()
        {
            Assert.AreEqual(0, Results.Results.Count, "There should be no Results");
        }
    }
}