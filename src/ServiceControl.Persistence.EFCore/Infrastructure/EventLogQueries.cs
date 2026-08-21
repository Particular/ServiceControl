namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;

static class EventLogQueries
{
    /// <summary>
    /// Every scalar field of every item the body shows, plus the total behind Total-Count so that retention
    /// deleting rows off the end of the log still moves the version. RelatedTo has no term of its own and
    /// does not need one: an item is inserted once and never updated, so the same Id always renders the same
    /// links.
    /// </summary>
    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<EventLogItemView> page, long totalCount, params (string Name, object? Value)[] query) =>
        QueryStatsInfo.Fresh(DataVersion.OverRows([("items", totalCount), .. query], page,
                item => [item.Id, item.Description, item.Severity, item.RaisedAt, item.Category, item.EventType]),
            totalCount);
}
