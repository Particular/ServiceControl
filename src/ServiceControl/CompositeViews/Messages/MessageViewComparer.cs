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

            switch (sortBy)
            {
                case "id":
                case "message_id":
                    return sortOrder == "asc" ? ByMessageIdAscending : ByMessageIdDescending;
                case "message_type":
                    return sortOrder == "asc" ? ByMessageTypeAscending : ByMessageTypeDescending;
                case "critical_time":
                    return sortOrder == "asc" ? ByCriticalTimeAscending : ByCriticalTimeDescending;
                case "delivery_time":
                    return sortOrder == "asc" ? ByDeliveryTimeAscending : ByDeliveryTimeDescending;
                case "time_sent":
                    return sortOrder == "asc" ? ByTimeSentAscending : ByTimeSentDescending;
                case "processing_time":
                    return sortOrder == "asc" ? ByProcessingTimeAscending : ByProcessingTimeDescending;
                case "processed_at":
                    return sortOrder == "asc" ? ByProcessedAtAscending : ByProcessedAtDescending;
                case "status":
                    return sortOrder == "asc" ? ByStatusAscending : ByStatusDescending;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sortBy));
            }
        }

        private static IComparer<MessagesView> ByMessageIdAscending = new Comparer((x, y) => string.Compare(x.MessageId, y.MessageId, StringComparison.Ordinal));
        private static IComparer<MessagesView> ByMessageIdDescending = new Comparer((x, y) => string.Compare(y.MessageId, x.MessageId, StringComparison.Ordinal));

        private static IComparer<MessagesView> ByMessageTypeAscending = new Comparer((x, y) => string.Compare(x.MessageType, y.MessageType, StringComparison.Ordinal));
        private static IComparer<MessagesView> ByMessageTypeDescending = new Comparer((x, y) => string.Compare(y.MessageType, x.MessageType, StringComparison.Ordinal));

        private static IComparer<MessagesView> ByTimeSentAscending = new Comparer((x, y) => Nullable.Compare(x.TimeSent, y.TimeSent));
        private static IComparer<MessagesView> ByTimeSentDescending = new Comparer((x, y) => Nullable.Compare(y.TimeSent, x.TimeSent));

        private static IComparer<MessagesView> ByCriticalTimeAscending = new Comparer((x, y) => x.CriticalTime.CompareTo(y.CriticalTime));
        private static IComparer<MessagesView> ByCriticalTimeDescending = new Comparer((x, y) => y.CriticalTime.CompareTo(x.CriticalTime));

        private static IComparer<MessagesView> ByDeliveryTimeAscending = new Comparer((x, y) => x.DeliveryTime.CompareTo(y.DeliveryTime));
        private static IComparer<MessagesView> ByDeliveryTimeDescending = new Comparer((x, y) => y.DeliveryTime.CompareTo(x.DeliveryTime));

        private static IComparer<MessagesView> ByProcessingTimeAscending = new Comparer((x, y) => x.ProcessingTime.CompareTo(y.ProcessingTime));
        private static IComparer<MessagesView> ByProcessingTimeDescending = new Comparer((x, y) => y.ProcessingTime.CompareTo(x.ProcessingTime));

        private static IComparer<MessagesView> ByProcessedAtAscending = new Comparer((x, y) => x.ProcessedAt.CompareTo(y.ProcessedAt));
        private static IComparer<MessagesView> ByProcessedAtDescending = new Comparer((x, y) => y.ProcessedAt.CompareTo(x.ProcessedAt));

        private static IComparer<MessagesView> ByStatusAscending = new Comparer((x, y) => x.Status.CompareTo(y.Status));
        private static IComparer<MessagesView> ByStatusDescending = new Comparer((x, y) => y.Status.CompareTo(x.Status));


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
        }
    }
}