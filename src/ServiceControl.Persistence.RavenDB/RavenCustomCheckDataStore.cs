namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
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
        public async Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail, CancellationToken cancellationToken = default)
        {
            var status = CheckStateChange.Unchanged;
            var id = MakeId(detail.GetDeterministicId());

            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var customCheck = await session.LoadAsync<CustomCheck>(id, cancellationToken);

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
            await session.StoreAsync(customCheck, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            return status;
        }

        static string MakeId(Guid id) => $"CustomChecks/{id}";

        public async Task<QueryResult<IList<CustomCheckView>>> GetStats(PagingInfo paging, string status = null, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var query =
                session.Query<CustomCheck, CustomChecksIndex>().Statistics(out var stats);

            query = AddStatusFilter(query, status);

            var results = await query
                .Paging(paging)
                .ToListAsync(cancellationToken);

            // Project to the read model right away: the API gets a copy, never the tracked document.
            var views = results
                .Select(ToCustomCheckView)
                .ToList();

            return new QueryResult<IList<CustomCheckView>>(views, stats.ToQueryStatsInfo());
        }

        static CustomCheckView ToCustomCheckView(CustomCheck customCheck) => new()
        {
            Id = customCheck.Id,
            CustomCheckId = customCheck.CustomCheckId,
            Category = customCheck.Category,
            Status = customCheck.Status,
            ReportedAt = customCheck.ReportedAt,
            FailureReason = customCheck.FailureReason,
            OriginatingEndpoint = customCheck.OriginatingEndpoint
        };

        public async Task DeleteCustomCheck(Guid id, CancellationToken cancellationToken = default)
        {
            var documentId = MakeId(id);
            using var session = await sessionProvider.OpenSession(new SessionOptions { NoTracking = true, NoCaching = true }, cancellationToken);
            await session.Advanced.RequestExecutor.ExecuteAsync(new DeleteDocumentCommand(documentId, null), session.Advanced.Context, token: cancellationToken);
        }

        public async Task<int> GetNumberOfFailedChecks(CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var failedCustomCheckCount = await session.Query<CustomCheck, CustomChecksIndex>().CountAsync(p => p.Status == Status.Fail, cancellationToken);

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