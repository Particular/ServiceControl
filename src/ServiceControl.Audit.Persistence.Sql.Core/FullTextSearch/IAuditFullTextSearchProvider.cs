namespace ServiceControl.Audit.Persistence.Sql.Core.FullTextSearch;

using Entities;

public interface IAuditFullTextSearchProvider
{
    IQueryable<ProcessedMessageEntity> ApplyFullTextSearch(
        IQueryable<ProcessedMessageEntity> query,
        string searchTerms);
}
