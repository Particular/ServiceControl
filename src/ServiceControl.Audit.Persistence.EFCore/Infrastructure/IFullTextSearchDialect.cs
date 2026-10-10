namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

using ServiceControl.Audit.Persistence.EFCore.Entities;

interface IFullTextSearchDialect
{
    IQueryable<AuditMessageEntity> Search(IQueryable<AuditMessageEntity> source, string searchTerms);
}
