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

            if (!SortByMap.TryGetValue(sortBy, out var comparer))
            {
                throw new ArgumentOutOfRangeException(nameof(sortBy));
            }

            return string.Equals(sortOrder, "desc", StringComparison.CurrentCultureIgnoreCase)
                ? comparer.Reverse()
                : comparer;
        }

        static Comparer ByMessageIdAscending = new Comparer((x, y) => string.Compare(x.MessageId, y.MessageId, StringComparison.Ordinal));
        static Comparer ByMessageTypeAscending = new Comparer((x, y) => string.Compare(x.MessageType, y.MessageType, StringComparison.Ordinal));
        static Comparer ByTimeSentAscending = new Comparer((x, y) => Nullable.Compare(x.TimeSent, y.TimeSent));
        static Comparer ByCriticalTimeAscending = new Comparer((x, y) => x.CriticalTime.CompareTo(y.CriticalTime));
        static Comparer ByDeliveryTimeAscending = new Comparer((x, y) => x.DeliveryTime.CompareTo(y.DeliveryTime));
        static Comparer ByProcessingTimeAscending = new Comparer((x, y) => x.ProcessingTime.CompareTo(y.ProcessingTime));
        static Comparer ByProcessedAtAscending = new Comparer((x, y) => x.ProcessedAt.CompareTo(y.ProcessedAt));
        static Comparer ByStatusAscending = new Comparer((x, y) => x.Status.CompareTo(y.Status));


        static IDictionary<string, Comparer> SortByMap = new Dictionary<string, Comparer>(StringComparer.InvariantCultureIgnoreCase)
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

            Func<MessagesView, MessagesView, int> comparerFunc;
        }

        class Reverse : IComparer<MessagesView>
        {
            public Reverse(IComparer<MessagesView> inner)
            {
                this.inner = inner;
            }

            public int Compare(MessagesView x, MessagesView y) => inner.Compare(y, x);
            readonly IComparer<MessagesView> inner;
        }
    }
}