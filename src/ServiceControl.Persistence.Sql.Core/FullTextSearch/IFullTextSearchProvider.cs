namespace ServiceControl.Persistence.Sql.Core.FullTextSearch;

using System.Linq;
using Entities;

public interface IFullTextSearchProvider
{
    IQueryable<FailedMessageEntity> ApplyFullTextSearch(
        IQueryable<FailedMessageEntity> query,
        string searchTerms);
}
