namespace ServiceControl.Operations
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class FailedErrorImportIndex : AbstractIndexCreationTask<FailedErrorImport>
    {
        public FailedErrorImportIndex()
        {
            Map = docs => from cc in docs
                          select new FailedErrorImport
                          {
                              Id = cc.Id,
                              Message = cc.Message
                          };

            DisableInMemoryIndexing = true;
        }
    }
}