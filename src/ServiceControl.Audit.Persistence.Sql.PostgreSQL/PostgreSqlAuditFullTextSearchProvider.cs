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
        // Use pre-computed tsvector column for fast full-text search
        // The "query" column is created via migration with weights:
        // - headers (weight A) for higher relevance
        // - body (weight B) for standard relevance
        return query.Where(pm =>
            EF.Property<NpgsqlTsVector>(pm, "query")
                .Matches(EF.Functions.WebSearchToTsQuery("english", searchTerms)));
    }
}
