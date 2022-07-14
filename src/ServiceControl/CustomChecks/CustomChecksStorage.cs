namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Infrastructure.Extensions;
    using Raven.Client;
    using Raven.Client.Linq;

    class RavenDbCustomCheckDataStore : ICustomChecksStorage
    {
        public RavenDbCustomCheckDataStore(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task UpdateCustomCheckStatus(CustomCheckDetail detail)
        {
            var publish = false;
            var id = DeterministicGuid.MakeId(detail.OriginatingEndpoint.Name, detail.OriginatingEndpoint.HostId.ToString(), detail.CustomCheckId);

            using (var session = store.OpenAsyncSession())
            {
                var customCheck = await session.LoadAsync<CustomCheck>(id)
                    .ConfigureAwait(false);

                if (customCheck == null ||
                    (customCheck.Status == Status.Fail && !detail.HasFailed) ||
                    (customCheck.Status == Status.Pass && detail.HasFailed))
                {
                    if (customCheck == null)
                    {
                        customCheck = new CustomCheck
                        {
                            Id = id
                        };
                    }

                    publish = true;
                }

                customCheck.CustomCheckId = detail.CustomCheckId;
                customCheck.Category = detail.Category;
                customCheck.Status = detail.HasFailed ? Status.Fail : Status.Pass;
                customCheck.ReportedAt = detail.ReportedAt;
                customCheck.FailureReason = detail.FailureReason;
                customCheck.OriginatingEndpoint = detail.OriginatingEndpoint;
                await session.StoreAsync(customCheck)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            if (publish)
            {
                if (detail.HasFailed)
                {
                    await domainEvents.Raise(new CustomCheckFailed
                    {
                        Id = id,
                        CustomCheckId = detail.CustomCheckId,
                        Category = detail.Category,
                        FailedAt = detail.ReportedAt,
                        FailureReason = detail.FailureReason,
                        OriginatingEndpoint = detail.OriginatingEndpoint
                    }).ConfigureAwait(false);
                }
                else
                {
                    await domainEvents.Raise(new CustomCheckSucceeded
                    {
                        Id = id,
                        CustomCheckId = detail.CustomCheckId,
                        Category = detail.Category,
                        SucceededAt = detail.ReportedAt,
                        OriginatingEndpoint = detail.OriginatingEndpoint
                    }).ConfigureAwait(false);
                }
            }

        }

        public async Task<QueryResult<IList<CustomCheck>>> GetStats(HttpRequestMessage request, string status = null)
        {
            using (var session = store.OpenAsyncSession())
            {
                var query =
                    session.Query<CustomCheck, CustomChecksIndex>().Statistics(out var stats);

                query = AddStatusFilter(query, status);

                var results = await query
                    .Paging(request)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return new QueryResult<IList<CustomCheck>>(results, new QueryStatsInfo(stats.IndexEtag, stats.TotalResults));
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

        IDocumentStore store;
        IDomainEvents domainEvents;
    }
}