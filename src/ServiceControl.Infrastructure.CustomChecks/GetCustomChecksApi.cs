namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Infrastructure.Extensions;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Settings;

    class GetCustomChecksApi : ScatterGatherApi<string, IList<CustomCheck>>
    {
        public GetCustomChecksApi(IDocumentStore documentStore, RemoteInstanceSettings settings, Func<HttpClient> httpClientFactory)
            : base(documentStore, settings, httpClientFactory)
        {
        }

        protected override async Task<QueryResult<IList<CustomCheck>>> LocalQuery(HttpRequestMessage request, string status)
        {
            using (var session = Store.OpenAsyncSession())
            {
                var query =
                    session.Query<CustomCheck, CustomChecksIndex>().Statistics(out var stats);

                query = AddStatusFilter(query, status);

                var results = await query
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

        protected override IList<CustomCheck> ProcessResults(HttpRequestMessage request, QueryResult<IList<CustomCheck>>[] results)
        {
            var dictionary = new Dictionary<Guid, CustomCheck>();

            //Flatten the list. Use the newest value for each check.
            foreach (var partialResult in results)
            {
                foreach (var customCheck in partialResult.Results)
                {
                    if (!dictionary.TryGetValue(customCheck.Id, out var existing) || customCheck.ReportedAt > existing.ReportedAt)
                    {
                        dictionary[customCheck.Id] = customCheck;
                    }
                }
            }

            var orderedEnumerable = dictionary.Values.OrderBy(x => x.Id);
            return ApplyPaging(request, orderedEnumerable);
        }

        static IList<CustomCheck> ApplyPaging(HttpRequestMessage request, IOrderedEnumerable<CustomCheck> orderedEnumerable)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);

            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return orderedEnumerable.Skip(skipResults).Take(maxResultsPerPage).ToList();
        }
    }
}