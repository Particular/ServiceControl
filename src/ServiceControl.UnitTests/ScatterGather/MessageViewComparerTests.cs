namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using Nancy;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

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

            Assert.AreEqual(compareResult, comparer.Compare(lower, higher), "Should sort messages the other way around");
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

            Assert.AreEqual(compareResult, comparer.Compare(lower, higher), "Should sort messages the other way around");
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

            Assert.AreEqual(compareResult, comparer.Compare(lower, higher), "Should sort messages the other way around");
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

            Assert.AreEqual(compareResult, comparer.Compare(lower, higher), "Should sort messages the other way around");
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

            Assert.AreEqual(compareResult, comparer.Compare(lower, higher), "Should sort messages the other way around");
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

            Assert.AreEqual(compareResult, comparer.Compare(lower, higher), "Should sort messages the other way around");
        }

        private IComparer<MessagesView> GetComparerFromRequest(string sort, string direction)
        {
            var queryStringParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(sort))
            {
                queryStringParts.Add($"sort={sort}");
            }

            if (!string.IsNullOrWhiteSpace(direction))
            {
                queryStringParts.Add($"direction={direction}");
            }

            var queryString = string.Join("&", queryStringParts);

            var request = new Request("GET", new Url($"http://get/messages?{queryString}"));
            return MessageViewComparer.FromRequest(request);
        }

        private const int ASCENDING = -1;
        private const int DESCENDING = 1;

        private static MessageStatus LowMessageStatus = (MessageStatus)1;
        private static MessageStatus HighMessageStatus = (MessageStatus)2;
        private static string LowMessageId = "a";
        private static string HighMessageId = "b";
        private static string LowMessageType = "A";
        private static string HighMessageType = "B";
        private static DateTime LowMessageSent = DateTime.UtcNow;
        private static DateTime HighMessageSent = LowMessageSent.AddMilliseconds(1);
        private static TimeSpan LowProcessingTime = TimeSpan.FromMilliseconds(5);
        private static TimeSpan HighProcessingTime = TimeSpan.FromMilliseconds(6);
    }
}