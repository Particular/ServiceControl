namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;

static class FailureGroupQueries
{
    public const int MaxGroups = 200;

    /// <summary>
    /// Aggregate with Title in the group key. Used by <see cref="GroupsDataStore.SingleGroup" />
    /// where a single group is fetched and the nvarchar(max) Title cost is negligible.
    /// </summary>
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
    /// Narrow aggregate on (GroupId, Type) only — the first step of the two-step group query.
    /// Title is functionally dependent on GroupId (GroupId = DeterministicGuid.MakeId(classifier.Name,
    /// classification)), so grouping without it is semantically equivalent. Keeping nvarchar(max) Title
    /// out of the group key avoids per-row LOB hashing/sorting and the large memory grant it demands.
    /// </summary>
    public static IQueryable<GroupSummary> AggregateGroupSummaries(this IQueryable<FailedMessageGroupEntity> groups, IQueryable<FailedMessageEntity> messages) =>
        from failureGroup in groups
        join message in messages on failureGroup.FailedMessageUniqueId equals message.UniqueMessageId
        group message by new { failureGroup.GroupId, failureGroup.Type }
        into aggregate
        select new GroupSummary
        {
            Id = aggregate.Key.GroupId,
            Type = aggregate.Key.Type,
            Count = aggregate.Count(),
            First = aggregate.Min(message => message.FirstTimeOfFailure),
            Last = aggregate.Max(message => message.LastTimeOfFailure)
        };
}

/// <summary>
/// Intermediate projection for step 1 of the two-step group aggregate. Title is fetched
/// separately in step 2 to keep nvarchar(max) out of the aggregate hash/sort.
/// </summary>
sealed class GroupSummary
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Count { get; set; }
    public DateTime First { get; set; }
    public DateTime Last { get; set; }
}
