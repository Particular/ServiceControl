namespace ServiceControl.Persistence.Sql.SqlServer;

using System.Linq;
using Core.Entities;
using Core.FullTextSearch;
using Microsoft.EntityFrameworkCore;

class SqlServerFullTextSearchProvider : IFullTextSearchProvider
{
    public IQueryable<FailedMessageEntity> ApplyFullTextSearch(
        IQueryable<FailedMessageEntity> query,
        string searchTerms)
    {
        // Search across both HeadersJson and Body columns using FREETEXT
        // Requires FULLTEXT index to be created on both columns (see SqlServerDbContext)
        return query.Where(fm =>
            EF.Functions.FreeText(fm.HeadersJson, searchTerms) ||
            EF.Functions.FreeText(fm.Body!, searchTerms));
    }
}
