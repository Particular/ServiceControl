namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL;

using Core.Entities;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

class PostgreSqlAuditFullTextSearchProvider : IAuditFullTextSearchProvider
{
    public IQueryable<ProcessedMessageEntity> ApplyFullTextSearch(
        IQueryable<ProcessedMessageEntity> query,
        string searchTerms)
    {
        // Use 'simple' configuration for exact matching (no stemming or stop words)
        // The GIN index on to_tsvector('simple', searchable_content) will be used
        return query.Where(pm =>
            EF.Functions.ToTsVector("simple", pm.SearchableContent!)
                .Matches(EF.Functions.PlainToTsQuery("simple", searchTerms)));
    }
}
