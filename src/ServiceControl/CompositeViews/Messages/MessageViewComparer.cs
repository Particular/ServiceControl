namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;
    using Nancy;

    static class MessageViewComparer
    {
        public static IComparer<MessagesView> FromRequest(Request request)
        {
            var queryString = (DynamicDictionary)request.Query;
            //Set the default sort to time_sent if not already set
            string sortBy = queryString.ContainsKey("sort")
                ? queryString["sort"]
                : "time_sent";

            //Set the default sort direction to `desc` if not already set
            string sortOrder = queryString.ContainsKey("direction")
                ? queryString["direction"]
                : "desc";

            Comparer comparer;

            if (!SortByMap.TryGetValue(sortBy, out comparer))
            {
                throw new ArgumentOutOfRangeException(nameof(sortBy));
            }

            return string.Equals(sortOrder, "desc", StringComparison.CurrentCultureIgnoreCase)
                ? comparer.Reverse()
                : comparer;
        }

        private static Comparer ByMessageIdAscending = new Comparer((x, y) => string.Compare(x.MessageId, y.MessageId, StringComparison.Ordinal));
        private static Comparer ByMessageTypeAscending = new Comparer((x, y) => string.Compare(x.MessageType, y.MessageType, StringComparison.Ordinal));
        private static Comparer ByTimeSentAscending = new Comparer((x, y) => Nullable.Compare(x.TimeSent, y.TimeSent));
        private static Comparer ByCriticalTimeAscending = new Comparer((x, y) => x.CriticalTime.CompareTo(y.CriticalTime));
        private static Comparer ByDeliveryTimeAscending = new Comparer((x, y) => x.DeliveryTime.CompareTo(y.DeliveryTime));
        private static Comparer ByProcessingTimeAscending = new Comparer((x, y) => x.ProcessingTime.CompareTo(y.ProcessingTime));
        private static Comparer ByProcessedAtAscending = new Comparer((x, y) => x.ProcessedAt.CompareTo(y.ProcessedAt));
        private static Comparer ByStatusAscending = new Comparer((x, y) => x.Status.CompareTo(y.Status));


        private static IDictionary<string, Comparer> SortByMap = new Dictionary<string, Comparer>(StringComparer.InvariantCultureIgnoreCase)
        {
            ["id"] = ByMessageIdAscending,
            ["message_id"] = ByMessageIdAscending,
            ["message_type"] = ByMessageTypeAscending,
            ["critical_time"] = ByCriticalTimeAscending,
            ["delivery_time"] = ByDeliveryTimeAscending,
            ["time_sent"] = ByTimeSentAscending,
            ["processing_time"] = ByProcessingTimeAscending,
            ["processed_at"] = ByProcessedAtAscending,
            ["status"] = ByStatusAscending
        };


        class Comparer : IComparer<MessagesView>
        {
            private Func<MessagesView, MessagesView, int> comparerFunc;

            public Comparer(Func<MessagesView, MessagesView, int> comparerFunc)
            {
                this.comparerFunc = comparerFunc;
            }

            public int Compare(MessagesView x, MessagesView y)
            {
                return comparerFunc(x, y);
            }

            public IComparer<MessagesView> Reverse()
            {
                return new Reverse(this);
            }
        }

        class Reverse : IComparer<MessagesView>
        {
            private readonly IComparer<MessagesView> inner;

            public Reverse(IComparer<MessagesView> inner)
            {
                this.inner = inner;
            }

            public int Compare(MessagesView x, MessagesView y) => inner.Compare(y, x);
        }
    }
}