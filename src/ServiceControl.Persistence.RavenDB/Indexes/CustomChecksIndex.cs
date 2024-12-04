namespace ServiceControl.Persistence
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Contracts.CustomChecks;

    class CustomChecksIndex : AbstractIndexCreationTask<CustomCheck>
    {
        public CustomChecksIndex()
        {
            Map = docs =>

                from cc in docs
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