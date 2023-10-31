namespace ServiceControl.Audit.Persistence.RavenDB.Indexes
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Audit.Auditing;

    public class FailedAuditImportIndex : AbstractIndexCreationTask<FailedAuditImport>
    {
        public FailedAuditImportIndex()
        {
            Map = docs =>
                from cc in docs
                select new FailedAuditImport
                {
                    Id = cc.Id,
                    Message = cc.Message
                };
        }
    }
}