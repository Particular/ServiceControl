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
        return query.Where(fm => EF.Functions.Match(fm.Query, searchTerms, MySqlMatchSearchMode.NaturalLanguage) > 0);
    }
}
