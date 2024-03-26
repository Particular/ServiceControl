namespace ServiceControl.Operations
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;

    class FailedErrorImportIndex : AbstractIndexCreationTask<FailedErrorImport>
    {
        public FailedErrorImportIndex() =>
            Map = docs =>

                from cc in docs
                select new FailedErrorImport
                {
                    Id = cc.Id
                };
    }
}