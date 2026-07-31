namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;

static class FailedMessageQueryFilters
{
    static readonly string[] ModifiedRangeSeparator = ["..."];

    const string InvalidModifiedRange =
        "Invalid modified date range, dates need to be in ISO8601 format and it needs to be a range eg. 2016-03-11T00:27:15.474Z...2016-03-16T03:27:15.474Z";

    public static IQueryable<FailedMessageEntity> FilterByStatus(this IQueryable<FailedMessageEntity> source, string? status)
    {
        if (status == null)
        {
            return source;
        }

        var includes = new List<FailedMessageStatus>();
        var excludes = new List<FailedMessageStatus>();

        foreach (var filter in status.Replace(" ", string.Empty).Split(','))
        {
            if (filter.StartsWith('-'))
            {
                if (Enum.TryParse<FailedMessageStatus>(filter[1..], true, out var excluded))
                {
                    excludes.Add(excluded);
                }

                continue;
            }

            if (Enum.TryParse<FailedMessageStatus>(filter, true, out var included))
            {
                includes.Add(included);
            }
        }

        if (includes.Count > 0)
        {
            source = source.Where(message => includes.Contains(message.Status));
        }

        foreach (var exclude in excludes)
        {
            // Captured per iteration so each exclusion closes over its own value.
            var excluded = exclude;
            source = source.Where(message => message.Status != excluded);
        }

        return source;
    }

    /// <summary>
    /// Accepts an ISO8601 range in the form "from...to".
    /// </summary>
    public static IQueryable<FailedMessageEntity> FilterByLastModifiedRange(this IQueryable<FailedMessageEntity> source, string? modified)
    {
        if (modified == null)
        {
            return source;
        }

        var filters = modified.Split(ModifiedRangeSeparator, StringSplitOptions.None);

        if (filters.Length != 2)
        {
            throw new Exception(InvalidModifiedRange);
        }

        DateTime from;
        DateTime to;

        try
        {
            from = DateTime.Parse(filters[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            to = DateTime.Parse(filters[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        catch (Exception)
        {
            throw new Exception(InvalidModifiedRange);
        }

        return source.Where(message => message.LastModified >= from && message.LastModified <= to);
    }

    public static IQueryable<FailedMessageEntity> FilterByQueueAddress(this IQueryable<FailedMessageEntity> source, string? queueAddress)
    {
        if (string.IsNullOrWhiteSpace(queueAddress))
        {
            return source;
        }

        var address = queueAddress.ToLowerInvariant();

        // The ToLower() here causes a full table scan!
        return source.Where(message => message.FailingEndpointAddress != null && message.FailingEndpointAddress.ToLower() == address);
    }

    public static IQueryable<FailedMessageEntity> Sort(this IQueryable<FailedMessageEntity> source, SortInfo? sortInfo)
    {
        var descending = sortInfo?.Direction != "asc";
        var sort = sortInfo?.Sort;

        if (sort == null || !SortInfo.AllowedSortOptions.Contains(sort))
        {
            sort = "time_sent";
        }

        return sort switch
        {
            "id" or "message_id" => source.OrderBy(message => message.MessageId, descending),
            "message_type" => source.OrderBy(message => message.MessageType, descending),
            "status" => source.OrderBy(message => message.Status, descending),
            "modified" => source.OrderBy(message => message.LastModified, descending),
            "time_of_failure" => source.OrderBy(message => message.LastTimeOfFailure, descending),
            _ => source.OrderBy(message => message.TimeSent, descending)
        };
    }

    public static IQueryable<FailedMessageEntity> Page(this IQueryable<FailedMessageEntity> source, PagingInfo pagingInfo) =>
        source.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

    public static async Task<QueryStatsInfo> ToQueryStatsInfo(this IQueryable<FailedMessageEntity> source, CancellationToken cancellationToken = default)
    {
        var stats = await source
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), Latest = group.Max(message => (DateTime?)message.LastModified) })
            .SingleOrDefaultAsync(cancellationToken);

        var count = stats?.Count ?? 0;
        var latest = stats?.Latest ?? DateTime.MinValue;

        return new QueryStatsInfo($"{count}-{latest.Ticks}", count, false);
    }

    static IOrderedQueryable<FailedMessageEntity> OrderBy<TKey>(this IQueryable<FailedMessageEntity> source, System.Linq.Expressions.Expression<Func<FailedMessageEntity, TKey>> keySelector, bool descending) =>
        descending
            ? source.OrderByDescending(keySelector).ThenByDescending(message => message.UniqueMessageId)
            : source.OrderBy(keySelector).ThenBy(message => message.UniqueMessageId);
}
