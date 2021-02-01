namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Contracts.Operations;
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

        IComparer<MessagesView> GetComparerFromRequest(string sort, string direction)
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

            var request = new HttpRequestMessage(new HttpMethod("GET"), $"http://get/messages?{queryString}");
            return MessageViewComparer.FromRequest(request);
        }

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