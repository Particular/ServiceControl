namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.MessageFailures.Api;

    [TestFixture]
    public class PreventReindexing
    {
        [Test]
        public void WeApproveThisBecauseChangingThisTypeWillLeadToAMassiveReindexWhichWeWantToAvoid()
        {
            var msgProperties = typeof(MessagesViewIndex.SortAndFilterOptions).GetProperties();
            var msgTimeSentType = msgProperties.Single(p => p.Name == "TimeSent").PropertyType;

            Assert.AreEqual(typeof(DateTime), msgTimeSentType);

            var failedMsgProperties = typeof(FailedMessageViewIndex.SortAndFilterOptions).GetProperties();
            var failedMsgTimeSentType = failedMsgProperties.Single(p => p.Name == "TimeSent").PropertyType;

            Assert.AreEqual(typeof(DateTime), failedMsgTimeSentType);
        }
    }
}
