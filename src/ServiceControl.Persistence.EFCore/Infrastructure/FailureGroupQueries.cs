namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

static class FailureGroupQueries
{
    public const int MaxGroups = 200;

    public static IQueryable<FailureGroupView> AggregateGroups(this IQueryable<FailedMessageGroupEntity> groups, IQueryable<FailedMessageEntity> messages) =>
        from failureGroup in groups
        join message in messages on failureGroup.FailedMessageUniqueId equals message.UniqueMessageId
        group message by new { failureGroup.GroupId, failureGroup.Title, failureGroup.Type }
        into aggregate
        select new FailureGroupView
        {
            Id = aggregate.Key.GroupId,
            Title = aggregate.Key.Title,
            Type = aggregate.Key.Type,
            Count = aggregate.Count(),
            First = aggregate.Min(message => message.FirstTimeOfFailure),
            Last = aggregate.Max(message => message.LastTimeOfFailure)
        };

    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<FailureGroupView> groups)
    {
        var latest = groups.Count == 0 ? DateTime.MinValue : groups.Max(group => group.Last);

        return new QueryStatsInfo(DataVersion.Compose(("groups", groups.Count), ("last", latest)), groups.Count, false);
    }
}
