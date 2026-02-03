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
        // Use SQL Server FREETEXT for natural language search on combined searchable content
        return query.Where(pm =>
            EF.Functions.FreeText(pm.SearchableContent!, searchTerms));
    }
}
