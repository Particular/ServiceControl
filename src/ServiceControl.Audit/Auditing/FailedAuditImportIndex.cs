namespace ServiceControl.Audit.Auditing
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;

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

            // TODO: RAVEN5 - This API is missing
            //DisableInMemoryIndexing = true;
        }
    }
}