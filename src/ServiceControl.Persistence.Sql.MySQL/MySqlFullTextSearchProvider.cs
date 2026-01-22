namespace ServiceControl.Persistence.Sql.MySQL;

using System.Linq;
using Core.Entities;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;

class MySqlFullTextSearchProvider : IFullTextSearchProvider
{
    public IQueryable<FailedMessageEntity> ApplyFullTextSearch(
        IQueryable<FailedMessageEntity> query,
        string searchTerms)
    {
        // Search across both HeadersJson and Body columns using FULLTEXT index
        // The multi-column FULLTEXT index is defined in MySqlDbContext
        return query.Where(fm =>
            EF.Functions.Match(new[] { fm.HeadersJson, fm.Body }, searchTerms, MySqlMatchSearchMode.NaturalLanguage) > 0);
    }
}
