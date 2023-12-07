namespace ServiceControl.Audit.Infrastructure;

using System.Collections.Generic;
using System.Net.Http;

public static class SortInfoExtension
{
    public static SortInfo GetSortInfo(this HttpRequestMessage request, string defaultSortDirection = "desc")
    {
        var direction = QueryStringExtension.GetQueryStringValue(request, "direction", defaultSortDirection);
        if (direction is not "asc" and not "desc")
        {
            direction = defaultSortDirection;
        }

        var sort = QueryStringExtension.GetQueryStringValue(request, "sort", "time_sent");
        if (!AllowableSortOptions.Contains(sort))
        {
            sort = "time_sent";
        }

        return new SortInfo(sort, direction);
    }

    static HashSet<string> AllowableSortOptions =
    [
        "processed_at",
        "id",
        "message_type",
        "time_sent",
        "critical_time",
        "delivery_time",
        "processing_time",
        "status",
        "message_id"
    ];
}