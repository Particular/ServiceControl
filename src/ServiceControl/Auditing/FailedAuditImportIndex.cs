namespace ServiceControl.Operations
{
    using System.Linq;
    using Raven.Client.Indexes;

    /// <summary>
    /// Legacy from the time the main instance handled also audits.
    /// </summary>
    public class FailedAuditImportIndex : AbstractIndexCreationTask<FailedAuditImport>
    {
        public FailedAuditImportIndex()
        {
            Map = docs => from cc in docs
                          select new FailedAuditImport
                          {
                              Id = cc.Id,
                              Message = cc.Message
                          };

            DisableInMemoryIndexing = true;
        }
    }
}