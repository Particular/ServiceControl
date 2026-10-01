namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class SqlServerFullTextSearchDialect : IFullTextSearchDialect
{
    public IQueryable<AuditMessageEntity> Search(IQueryable<AuditMessageEntity> source, string searchTerms) =>
        source.Where(message =>
            EF.Functions.FreeText(message.HeadersJson, searchTerms) ||
            EF.Functions.FreeText(message.BodyText!, searchTerms));
}
