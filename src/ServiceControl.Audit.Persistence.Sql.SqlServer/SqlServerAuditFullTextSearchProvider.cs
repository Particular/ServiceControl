namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.Entities;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;

class SqlServerAuditFullTextSearchProvider : IAuditFullTextSearchProvider
{
    public IQueryable<ProcessedMessageEntity> ApplyFullTextSearch(
        IQueryable<ProcessedMessageEntity> query,
        string searchTerms)
    {
        // Use SQL Server FREETEXT for natural language search
        // Requires FULLTEXT index on HeadersJson and Body columns
        return query.Where(pm =>
            EF.Functions.FreeText(pm.HeadersJson, searchTerms) ||
            EF.Functions.FreeText(pm.Body!, searchTerms));
    }
}
