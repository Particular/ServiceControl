namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SqlServerFullTextSearchDialect : IFullTextSearchDialect
{
    // FREETEXT ORs the terms itself, so the search string needs no parsing here. Both columns are
    // covered by the index the AddFullTextSearch migration creates, and a NULL body simply does not
    // match.
    public IQueryable<FailedMessageEntity> Search(IQueryable<FailedMessageEntity> source, string searchTerms) =>
        source.Where(message =>
            EF.Functions.FreeText(message.HeadersJson, searchTerms) ||
            EF.Functions.FreeText(message.BodyText!, searchTerms));
}
