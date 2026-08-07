namespace ServiceControl.Persistence.EFCore.Infrastructure;

using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// The provider specific full text predicate over the failed messages table. Implementations must
/// produce SQL the index created by the AddFullTextSearch migration can serve: on PostgreSQL that
/// means reproducing the indexed expression exactly, because an expression the planner cannot match
/// turns every search into a sequential scan rather than failing.
/// </summary>
public interface IFullTextSearchDialect
{
    /// <summary>
    /// Terms are ORed, matching the RavenDB persister, whose Search defaults to SearchOperator.Or.
    /// Callers guarantee the terms are not blank.
    /// </summary>
    IQueryable<FailedMessageEntity> Search(IQueryable<FailedMessageEntity> source, string searchTerms);
}
