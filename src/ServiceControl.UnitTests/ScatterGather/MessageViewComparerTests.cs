namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using Persistence.Infrastructure;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence;

    [TestFixture]
    class MessageViewComparerTests
    {
        [Test]
        [TestCase("", DESCENDING)]
        [TestCase("asc", ASCENDING)]
        [TestCase("desc", DESCENDING)]
        public void SortByStatus(string direction, int compareResult)
        {
            var comparer = GetComparerFromRequest("status", direction);

            var lower = new MessagesView
            {
                Status = LowMessageStatus
            };

            var higher = new MessagesView
            {
                Status = HighMessageStatus
            };

            Assert.That(comparer.Compare(lower, higher), Is.EqualTo(compareResult), "Should sort messages the other way around");
        }

        [Test]
        [TestCase("", DESCENDING)]
        [TestCase("asc", ASCENDING)]
        [TestCase("desc", DESCENDING)]
        public void SortById(string direction, int compareResult)
        {
            var comparer = GetComparerFromRequest("id", direction);

            var lower = new MessagesView
            {
                MessageId = LowMessageId
            };

            var higher = new MessagesView
            {
                MessageId = HighMessageId
            };

            Assert.That(comparer.Compare(lower, higher), Is.EqualTo(compareResult), "Should sort messages the other way around");
        }

        [Test]
        [TestCase("", DESCENDING)]
        [TestCase("asc", ASCENDING)]
        [TestCase("desc", DESCENDING)]
        public void SortByMessageType(string direction, int compareResult)
        {
            var comparer = GetComparerFromRequest("message_type", direction);

            var lower = new MessagesView
            {
                MessageType = LowMessageType
            };

            var higher = new MessagesView
            {
                MessageType = HighMessageType
            };

            Assert.That(comparer.Compare(lower, higher), Is.EqualTo(compareResult), "Should sort messages the other way around");
        }

        [Test]
        [TestCase("", DESCENDING)]
        [TestCase("asc", ASCENDING)]
        [TestCase("desc", DESCENDING)]
        public void SortByTimeSent(string direction, int compareResult)
        {
            var comparer = GetComparerFromRequest("time_sent", direction);

            var lower = new MessagesView
            {
                TimeSent = LowMessageSent
            };

            var higher = new MessagesView
            {
                TimeSent = HighMessageSent
            };

            Assert.That(comparer.Compare(lower, higher), Is.EqualTo(compareResult), "Should sort messages the other way around");
        }

        [Test]
        [TestCase("", DESCENDING)]
        [TestCase("asc", ASCENDING)]
        [TestCase("desc", DESCENDING)]
        public void SortByProcessingTime(string direction, int compareResult)
        {
            var comparer = GetComparerFromRequest("processing_time", direction);

            var lower = new MessagesView
            {
                ProcessingTime = LowProcessingTime
            };

            var higher = new MessagesView
            {
                ProcessingTime = HighProcessingTime
            };

            Assert.That(comparer.Compare(lower, higher), Is.EqualTo(compareResult), "Should sort messages the other way around");
        }

        [Test]
        [TestCase("", DESCENDING)]
        [TestCase("asc", ASCENDING)]
        [TestCase("desc", DESCENDING)]
        public void DefaultSortIsByTimeSent(string direction, int compareResult)
        {
            var comparer = GetComparerFromRequest(null, direction);

            var lower = new MessagesView
            {
                Status = HighMessageStatus,
                MessageId = HighMessageId,
                MessageType = HighMessageType,
                ProcessingTime = HighProcessingTime,
                // NOTE: The above are inverted to catch other sort schemes
                TimeSent = LowMessageSent
            };

            var higher = new MessagesView
            {
                Status = LowMessageStatus,
                MessageId = LowMessageId,
                MessageType = LowMessageType,
                ProcessingTime = LowProcessingTime,
                // NOTE: The above are inverted to catch other sort schemes
                TimeSent = HighMessageSent
            };

            Assert.That(comparer.Compare(lower, higher), Is.EqualTo(compareResult), "Should sort messages the other way around");
        }

        IComparer<MessagesView> GetComparerFromRequest(string sort, string direction) => MessageViewComparer.FromSortInfo(new SortInfo(sort, direction));

        const int ASCENDING = -1;
        const int DESCENDING = 1;

        static MessageStatus LowMessageStatus = (MessageStatus)1;
        static MessageStatus HighMessageStatus = (MessageStatus)2;
        static string LowMessageId = "a";
        static string HighMessageId = "b";
        static string LowMessageType = "A";
        static string HighMessageType = "B";
        static DateTime LowMessageSent = DateTime.UtcNow;
        static DateTime HighMessageSent = LowMessageSent.AddMilliseconds(1);
        static TimeSpan LowProcessingTime = TimeSpan.FromMilliseconds(5);
        static TimeSpan HighProcessingTime = TimeSpan.FromMilliseconds(6);
    }
}