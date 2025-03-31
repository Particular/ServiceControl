namespace ServiceControl.Audit.Persistence.RavenDB.Extensions
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using Indexes;
    using ServiceControl.Audit.Infrastructure;

    static class RavenQueryExtensions
    {
        public static IQueryable<MessagesViewIndex.SortAndFilterOptions> IncludeSystemMessagesWhere(
            this IQueryable<MessagesViewIndex.SortAndFilterOptions> source, bool includeSystemMessages)
        {
            if (!includeSystemMessages)
            {
                return source.Where(m => !m.IsSystemMessage);
            }

            return source;
        }

        public static IQueryable<MessagesViewIndex.SortAndFilterOptions> FilterBySentTimeRange(this IQueryable<MessagesViewIndex.SortAndFilterOptions> source, string range)
        {
            if (string.IsNullOrWhiteSpace(range))
            {
                return source;
            }

            var filters = range.Split(SplitChars, StringSplitOptions.None);
            DateTime from, to;
            try
            {

                if (filters.Length == 2)
                {
                    from = DateTime.Parse(filters[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    to = DateTime.Parse(filters[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    source.Where(m => m.TimeSent >= from && m.TimeSent <= to);

                }
                else
                {
                    from = DateTime.Parse(filters[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    source.Where(m => m.TimeSent >= from);
                }
            }
            catch (Exception)
            {
                throw new Exception(
                    "Invalid sent time date range, dates need to be in ISO8601 format and it needs to be a range eg. 2016-03-11T00:27:15.474Z...2016-03-16T03:27:15.474Z");
            }

            return source;
        }

        static string[] SplitChars =
        {
            "..."
        };

        public static IQueryable<TSource> Paging<TSource>(this IQueryable<TSource> source, PagingInfo pagingInfo)
            => source.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize);

        public static IQueryable<TSource> Sort<TSource>(this IQueryable<TSource> source, SortInfo sortInfo)
            where TSource : MessagesViewIndex.SortAndFilterOptions
        {
            Expression<Func<TSource, object>> keySelector = sortInfo.Sort switch
            {
                "id" or "message_id" => m => m.MessageId,
                "message_type" => m => m.MessageType,
                "critical_time" => m => m.CriticalTime,
                "delivery_time" => m => m.DeliveryTime,
                "processing_time" => m => m.ProcessingTime,
                "processed_at" => m => m.ProcessedAt,
                "status" => m => m.Status,
                _ => m => m.TimeSent,
            };

            if (sortInfo.Direction == "asc")
            {
                return source.OrderBy(keySelector);
            }

            return source.OrderByDescending(keySelector);
        }
    }
}