namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Linq;
    using MessageFailures.Api;
    using NUnit.Framework;
    using ServiceControl.Persistence;

    [TestFixture]
    public class PreventReindexing
    {
        [Test]
        public void WeApproveThisBecauseChangingThisTypeWillLeadToAMassiveReindexWhichWeWantToAvoid()
        {
            var msgProperties = typeof(MessagesViewIndex.SortAndFilterOptions).GetProperties();
            var msgTimeSentType = msgProperties.Single(p => p.Name == "TimeSent").PropertyType;

            Assert.That(msgTimeSentType, Is.EqualTo(typeof(DateTime)));

            var failedMsgProperties = typeof(FailedMessageViewIndex.SortAndFilterOptions).GetProperties();
            var failedMsgTimeSentType = failedMsgProperties.Single(p => p.Name == "TimeSent").PropertyType;

            Assert.That(failedMsgTimeSentType, Is.EqualTo(typeof(DateTime)));
        }
    }
}