namespace ServiceControl.Persistence.InMemory
{
    using System;
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

        public Task DeleteCustomCheck(Guid id)
        {
            var toRemove = storage.FirstOrDefault(x => x.Key == id);
            if (storage.ContainsKey(toRemove.Key))
            {
                storage.Remove(toRemove.Key);
            }
            return Task.CompletedTask;
        }

        Dictionary<Guid, CustomCheckDetail> storage = new Dictionary<Guid, CustomCheckDetail>();
    }
}