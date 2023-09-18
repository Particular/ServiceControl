namespace ServiceControl.Persistence
{
    using System.Linq;
    using ServiceControl.Contracts.CustomChecks;
    using Raven.Client.Documents.Indexes;

    class CustomChecksIndex : AbstractIndexCreationTask<CustomCheck>
    {
        public CustomChecksIndex()
        {
            Map = docs => from cc in docs
                          select new CustomCheck
                          {
                              Status = cc.Status,
                              ReportedAt = cc.ReportedAt,
                              Category = cc.Category,
                              CustomCheckId = cc.CustomCheckId
                          };
        }
    }
}