namespace ServiceControl.Persistence.InMemory
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;
    using Infrastructure;
    using ServiceControl.CustomChecks;
    using ServiceControl.Persistence;

    class InMemoryCustomCheckDataStore : ICustomChecksDataStore
    {
        public Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail)
        {
            var id = detail.GetDeterministicId();
            if (storage.ContainsKey(id))
            {
                var storedCheck = storage[id];
                if (storedCheck.HasFailed == detail.HasFailed)
                {
                    return Task.FromResult(CheckStateChange.Unchanged);
                }

                storedCheck.HasFailed = detail.HasFailed;
            }
            else
            {
                storage.Add(id, detail);
            }

            return Task.FromResult(CheckStateChange.Changed);
        }

        public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null)
        {
            var result = storage
                .Skip(paging.Offset)
                .Take(paging.PageSize)
                .Select(x => new CustomCheck
                {
                    Category = x.Value.Category,
                    Status = x.Value.HasFailed ? Status.Fail : Status.Pass,
                    FailureReason = x.Value.FailureReason,
                    ReportedAt = x.Value.ReportedAt,
                    CustomCheckId = x.Value.CustomCheckId,
                    OriginatingEndpoint = x.Value.OriginatingEndpoint
                })
                .ToList();

            var stats = new QueryStatsInfo("", storage.Count);

            return Task.FromResult(new QueryResult<IList<CustomCheck>>(result, stats));
        }

        Dictionary<string, CustomCheckDetail> storage = new Dictionary<string, CustomCheckDetail>();
    }
}