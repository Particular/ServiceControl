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
        // Convert text to tsvector at query time, use websearch_to_tsquery for user-friendly search
        return query.Where(fm => EF.Functions.ToTsVector("english", fm.Query ?? "")
            .Matches(EF.Functions.WebSearchToTsQuery("english", searchTerms)));
    }
}
