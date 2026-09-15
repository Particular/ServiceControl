namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;

static class FailureGroupQueries
{
    public const int MaxGroups = 200;

    /// <summary>
    /// The group aggregate: membership rows joined to their message, grouped by (GroupId, Type),
    /// with Count/First/Last per group. Title is fetched by <c>aggregate.First().Title</c>, which EF
    /// translates into a correlated TOP(1)/LIMIT 1 subquery per output group — bounded by
    /// <see cref="MaxGroups" /> index seeks, never a scan of the group's members, and never part of
    /// the group key, so the nvarchar(max)/text Title is neither hashed nor sorted per joined row.
    /// That title subquery stays cheap only while the classifier index covers it: SQL Server's
    /// (Type, GroupId) INCLUDE (Title) index also carries the clustered key (FailedMessageUniqueId)
    /// implicitly, while PostgreSQL's equivalent has to include both Title and FailedMessageUniqueId
    /// explicitly, because PostgreSQL indexes do not contain the primary key implicitly. Without the
    /// cover, the planner falls back to a sequential scan per output group.
    /// </summary>
    public static IQueryable<FailureGroupView> AggregateGroups(this IQueryable<FailedMessageGroupEntity> groups, IQueryable<FailedMessageEntity> messages) =>
        groups
            .Join(messages,
                failureGroup => failureGroup.FailedMessageUniqueId, message => message.UniqueMessageId,
                (failureGroup, message) => new { failureGroup, message })
            .GroupBy(t => new { t.failureGroup.GroupId, t.failureGroup.Type }, t => new
            {
                t.failureGroup.Title,
                t.message.FirstTimeOfFailure,
                t.message.LastTimeOfFailure,
            })
            .Select(aggregate => new FailureGroupView
            {
                Id = aggregate.Key.GroupId,
                // GroupId is DeterministicGuid.MakeId(classifier.Name, classification), so Title is
                // functionally dependent on the group key: the first row's Title is the group's Title.
                Title = aggregate.First().Title,
                Type = aggregate.Key.Type,
                Count = aggregate.Count(),
                First = aggregate.Min(message => message.FirstTimeOfFailure),
                Last = aggregate.Max(message => message.LastTimeOfFailure)
            });
}