namespace ServiceControl.Operations
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;

    class FailedAuditImportIndex : AbstractIndexCreationTask<FailedAuditImport>
    {
        public FailedAuditImportIndex()
        {
            Map = docs => from cc in docs
                          select new FailedAuditImport
                          {
                              Id = cc.Id,
                              Message = cc.Message
                          };
        }
    }
}