namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class RavenDbCustomCheckDataStore : ICustomChecksDataStore
    {
        public RavenDbCustomCheckDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail)
        {
            var status = CheckStateChange.Unchanged;
            var id = detail.GetDeterministicId();
            var key = MakeId(id);

            using (var session = store.OpenAsyncSession())
            {
                var customCheck = await session.LoadAsync<CustomCheck>(id);

                if (customCheck == null ||
                    (customCheck.Status == Status.Fail && !detail.HasFailed) ||
                    (customCheck.Status == Status.Pass && detail.HasFailed))
                {
                    if (customCheck == null)
                    {
                        customCheck = new CustomCheck
                        {
                            Id = id.ToString(), // No prefix for backward compatibility
                        };
                    }

                    status = CheckStateChange.Changed;
                }

                customCheck.CustomCheckId = detail.CustomCheckId;
                customCheck.Category = detail.Category;
                customCheck.Status = detail.HasFailed ? Status.Fail : Status.Pass;
                customCheck.ReportedAt = detail.ReportedAt;
                customCheck.FailureReason = detail.FailureReason;
                customCheck.OriginatingEndpoint = detail.OriginatingEndpoint;
                await session.StoreAsync(customCheck, key); // TODO: @ramonsmits Not passing key here results in the DELETE operation not working. Other option was passing this full key with prefix as the ID but that likely breaks compatibility with previous schema
                await session.SaveChangesAsync();
            }

            return status;
        }

        string MakeId(Guid id)
        {
            return store.Conventions.DefaultFindFullDocumentKeyFromNonStringIdentifier(id, typeof(CustomCheck), false);
        }

        public async Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null)
        {
            using (var session = store.OpenAsyncSession())
            {
                var query =
                    session.Query<CustomCheck, CustomChecksIndex>().Statistics(out var stats);

                query = AddStatusFilter(query, status);

                var results = await query
                    .Paging(paging)
                    .ToListAsync();

                return new QueryResult<IList<CustomCheck>>(results, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults, stats.IsStale));
            }
        }

        public async Task DeleteCustomCheck(Guid id)
        {
            var key = MakeId(id);
            await store.AsyncDatabaseCommands.DeleteAsync(key, etag: null);
        }

        public async Task<int> GetNumberOfFailedChecks()
        {
            using (var session = store.OpenAsyncSession())
            {
                var failedCustomCheckCount = await session.Query<CustomCheck, CustomChecksIndex>().CountAsync(p => p.Status == Status.Fail);

                return failedCustomCheckCount;
            }
        }

        static IRavenQueryable<CustomCheck> AddStatusFilter(IRavenQueryable<CustomCheck> query, string status)
        {
            if (status == null)
            {
                return query;
            }

            if (status == "fail")
            {
                query = query.Where(c => c.Status == Status.Fail);
            }

            if (status == "pass")
            {
                query = query.Where(c => c.Status == Status.Pass);
            }

            return query;
        }

        public Task Setup() => Task.CompletedTask;

        IDocumentStore store;
    }
}
