namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;

static class FailureGroupQueries
{
    public const int MaxGroups = 200;

    /// <summary>
    /// The group aggregate: membership rows joined to their message, grouped by (GroupId, Type),
    /// with Count/First/Last per group.
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