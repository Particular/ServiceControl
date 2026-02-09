namespace ServiceControl.Persistence.Sql.PostgreSQL;

using System.Linq;
using Core.Entities;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;

class PostgreSqlFullTextSearchProvider : IFullTextSearchProvider
{
    public IQueryable<FailedMessageEntity> ApplyFullTextSearch(
        IQueryable<FailedMessageEntity> query,
        string searchTerms)
    {
        // Use pre-computed tsvector column for fast full-text search
        // The Query column is a computed tsvector that combines headers (weight A) and body (weight B)
        return query.Where(fm =>
            EF.Property<NpgsqlTypes.NpgsqlTsVector>(fm, "Query")
                .Matches(EF.Functions.WebSearchToTsQuery("english", searchTerms)));
    }
}
