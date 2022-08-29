namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Net.Http;

    class SortInfo
    {
        public string Direction { get; }
        public string Sort { get; }

        public SortInfo(string sort, string direction)
        {
            Sort = sort;
            Direction = direction;
        }
    }

    static class SortInfoExtension
    {
        public static SortInfo GetSortInfo(this HttpRequestMessage request, string defaultSortDirection = "desc")
        {
            var direction = request.GetQueryStringValue("direction", defaultSortDirection);
            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }

            var sort = request.GetQueryStringValue("sort", "time_sent");
            if (!AllowableSortOptions.Contains(sort))
            {
                sort = "time_sent";
            }

            return new SortInfo(sort, direction);
        }

        static HashSet<string> AllowableSortOptions = new HashSet<string>
        {
            "processed_at",
            "id",
            "message_type",
            "time_sent",
            "critical_time",
            "delivery_time",
            "processing_time",
            "status",
            "message_id"
        };
    }
}