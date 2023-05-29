namespace ServiceControl.Persistence
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.CustomChecks;

    public class CustomChecksIndex : AbstractIndexCreationTask<CustomCheck>
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

            DisableInMemoryIndexing = true;
        }
    }
}