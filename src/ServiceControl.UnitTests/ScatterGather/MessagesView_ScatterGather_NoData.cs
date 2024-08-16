namespace ServiceControl.UnitTests.ScatterGather
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence.Infrastructure;

    [TestFixture]
    class MessagesView_ScatterGather_NoData : MessageView_ScatterGatherTest
    {
        protected override QueryResult<IList<MessagesView>>[] GetData()
        {
            return new[] { QueryResult<IList<MessagesView>>.Empty(), QueryResult<IList<MessagesView>>.Empty() };
        }

        [Test]
        public void NoResults()
        {
            Assert.That(Results.Results.Count, Is.EqualTo(0), "There should be no Results");
        }
    }
}