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
        // Use FREETEXT for natural language full-text search
        // EF.Functions.FreeText is available in EF Core for SQL Server
        return query.Where(fm => EF.Functions.FreeText(fm.Query ?? "", searchTerms));
    }
}
