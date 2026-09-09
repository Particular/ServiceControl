namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Primitives;
    using Persistence.Infrastructure;

    static class HttpResponseExtensions
    {
        /// <summary>
        /// Names the instances whose data the response is missing, as "instanceId:reason" entries, so a client
        /// can tell an incomplete page from a complete one. Absent when every instance answered.
        /// </summary>
        /// <remarks>
        /// A header rather than a JSON field on purpose: the composite endpoints return a bare array (or a single
        /// object), so the only place for this in the body would be an envelope around it. That changes the
        /// response schema and would need a new API for every client that reads these endpoints today.
        /// The header keeps the API backward compatible, the way Total-Count, ETag and Link already do for the
        /// rest of the list metadata; a client that does not know the header simply ignores it.
        /// </remarks>
        public const string IncompleteResultsHeader = "X-Particular-Incomplete-Results";

        public static void WithScatterGatherResult<T>(this HttpResponse response, QueryResult<T> result, PagingInfo pagingInfo)
            where T : class
        {
            response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);
            response.WithIncompleteResults(result.IncompleteInstances);
        }

        public static void WithIncompleteResults(this HttpResponse response, IReadOnlyList<IncompleteInstance> incomplete)
        {
            if (incomplete.Count == 0)
            {
                return;
            }

            response.WithHeader(IncompleteResultsHeader, string.Join(", ", incomplete.Select(instance => $"{instance.InstanceId}:{instance.Reason switch
            {
                QueryFailure.TimedOut => "timeout",
                QueryFailure.Unavailable => "unavailable",
                QueryFailure.Failed => "error",
                _ => "error"
            }}")));
        }

        public static void WithTotalCount(this HttpResponse response, long totalCount) => response.WithHeader("Total-Count", totalCount.ToString(CultureInfo.InvariantCulture));

        public static void WithEtag(this HttpResponse response, DataVersion version)
        {
            if (!version.HasValue)
            {
                return;
            }

            // Quotes are required by RFC 9110. Without them EntityTagHeaderValue cannot parse the tag and
            // NotModifiedStatusHttpHandler never matches a client's If-None-Match.
            response.Headers.ETag = $"W/\"{version}\"";
        }

        public static void WithQueryStatsInfo(this HttpResponse response, QueryStatsInfo queryStatsInfo)
        {
            response.WithTotalCount(queryStatsInfo.TotalCount);
            response.WithEtag(queryStatsInfo.Version);
        }

        static void WithHeader(this HttpResponse response, string header, StringValues value) => response.Headers.Append(header, value);

        public static void WithPagingLinks(this HttpResponse response, PagingInfo pageInfo, long highestTotalCountOfAllInstances, long totalResults)
        {
            // The size asked for, not the default
            if (totalResults <= pageInfo.PageSize)
            {
                return;
            }

            var links = new List<string>(4);
            var lastPage = (int)Math.Ceiling((double)highestTotalCountOfAllInstances / pageInfo.PageSize);

            // No need to add a Link header if page does not exist!
            if (pageInfo.Page > lastPage)
            {
                return;
            }

            var path = Uri.UnescapeDataString(response.HttpContext.Request.Path)
                .Replace("/api/", string.Empty); // NOTE: Strips off the /api/ for backwards compat

            // Currently not making a copy of the query collection for every add link call because the code assumes
            // AddLink will always set the page property and thus override previously assigned values
            var originalQueryCollection = response.HttpContext.Request.Query.Where(pair => pair.Key != "page")
                .ToDictionary();

            if (pageInfo.Page != 1)
            {
                AddLink(links, 1, "first", path, originalQueryCollection);
            }

            if (pageInfo.Page > 1)
            {
                AddLink(links, pageInfo.Page - 1, "prev", path, originalQueryCollection);
            }

            if (pageInfo.Page != lastPage)
            {
                AddLink(links, lastPage, "last", path, originalQueryCollection);
            }

            if (pageInfo.Page < lastPage)
            {
                AddLink(links, pageInfo.Page + 1, "next", path, originalQueryCollection);
            }

            response.WithHeader("Link", new StringValues(links.ToArray()));
        }

        static void AddLink(ICollection<string> links, int page, string rel, string uriPath, Dictionary<string, StringValues> queryParams)
        {
            queryParams["page"] = new StringValues(page.ToString(CultureInfo.InvariantCulture));
            var pathWithQuery = QueryHelpers.AddQueryString(uriPath, queryParams);
            links.Add($"<{pathWithQuery}>; rel=\"{rel}\"");
        }

        public static void WithQueryStatsAndPagingInfo(this HttpResponse response, QueryStatsInfo queryStats, PagingInfo pagingInfo)
        {
            response.WithPagingLinksAndTotalCount(pagingInfo, queryStats.TotalCount, queryStats.HighestTotalCountOfAllTheInstances);
            response.WithEtag(queryStats.Version);
        }

        public static void WithPagingLinksAndTotalCount(this HttpResponse response,
            PagingInfo pagingInfo, long totalCount, long? highestTotalCountOfAllInstances = null)
        {
            response.WithTotalCount(totalCount);
            response.WithPagingLinks(pagingInfo, highestTotalCountOfAllInstances ?? totalCount, totalCount);
        }
    }
}