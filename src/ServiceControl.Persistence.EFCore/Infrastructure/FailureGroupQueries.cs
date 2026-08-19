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

    /// <summary>
    /// Title and Type cannot move within a row, because AggregateGroups groups by them, so a change to
    /// either is a different row rather than a changed one.
    /// </summary>
    public static QueryStatsInfo ToQueryStatsInfo(this IReadOnlyCollection<FailureGroupView> groups) =>
        new(DataVersion.Compose(
                ("groups", groups.Count),
                ("state", string.Join("|", groups.Select(group => FormattableString.Invariant(
                    $"{group.Id}.{group.Title}.{group.Type}.{group.Count}.{group.Comment}.{group.First.Ticks}.{group.Last.Ticks}"))))),
            groups.Count,
            false);
}
