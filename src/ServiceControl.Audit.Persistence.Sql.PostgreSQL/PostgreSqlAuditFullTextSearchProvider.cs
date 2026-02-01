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
        // Use pre-computed tsvector column with 'simple' configuration for exact matching
        // The "query" column combines headers_json and body without stemming or stop words
        return query.Where(pm =>
            EF.Property<NpgsqlTsVector>(pm, "query")
                .Matches(EF.Functions.PlainToTsQuery("simple", searchTerms)));
    }
}
