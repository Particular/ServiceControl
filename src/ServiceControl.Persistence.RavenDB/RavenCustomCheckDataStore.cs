namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Session;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    class RavenCustomCheckDataStore(IRavenSessionProvider sessionProvider) : ICustomChecksDataStore
    {
        public async Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail)
        {
            var status = CheckStateChange.Unchanged;
            var id = MakeId(detail.GetDeterministicId());

            using var session = await sessionProvider.OpenSession();
            var customCheck = await session.LoadAsync<CustomCheck>(id);

            if (customCheck == null ||
                (customCheck.Status == Status.Fail && !detail.HasFailed) ||
                (customCheck.Status == Status.Pass && detail.HasFailed))
            {
                customCheck ??= new CustomCheck { Id = id };

                status = CheckStateChange.Changed;
            }

            customCheck.CustomCheckId = detail.CustomCheckId;
            customCheck.Category = detail.Category;
            customCheck.Status = detail.HasFailed ? Status.Fail : Status.Pass;
            customCheck.ReportedAt = detail.ReportedAt;
            customCheck.FailureReason = detail.FailureReason;
            customCheck.OriginatingEndpoint = detail.OriginatingEndpoint;
            await session.StoreAsync(customCheck);
            await session.SaveChangesAsync();

            return status;
        }

        static string MakeId(Guid id) => $"CustomChecks/{id}";

        public async Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null)
        {
            using var session = await sessionProvider.OpenSession();
            var query =
                session.Query<CustomCheck, CustomChecksIndex>().Statistics(out var stats);

            query = AddStatusFilter(query, status);

            var results = await query
                .Paging(paging)
                .ToListAsync();

            return new QueryResult<IList<CustomCheck>>(results, new QueryStatsInfo($"{stats.ResultEtag}", stats.TotalResults, stats.IsStale));
        }

        public async Task DeleteCustomCheck(Guid id)
        {
            var documentId = MakeId(id);
            using var session = await sessionProvider.OpenSession(new SessionOptions { NoTracking = true, NoCaching = true });
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(documentId, null), session.Advanced.Context);
        }

        public async Task<int> GetNumberOfFailedChecks()
        {
            using var session = await sessionProvider.OpenSession();
            var failedCustomCheckCount = await session.Query<CustomCheck, CustomChecksIndex>().CountAsync(p => p.Status == Status.Fail);

            return failedCustomCheckCount;
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
    }
}