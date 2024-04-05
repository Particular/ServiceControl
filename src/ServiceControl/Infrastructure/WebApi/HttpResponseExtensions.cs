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
        public static void WithTotalCount(this HttpResponse response, long totalCount) => response.WithHeader("Total-Count", totalCount.ToString(CultureInfo.InvariantCulture));

        public static void WithEtag(this HttpResponse response, StringValues value) => response.Headers.ETag = value;

        public static void WithQueryStatsInfo(this HttpResponse response, QueryStatsInfo queryStatsInfo)
        {
            response.WithTotalCount(queryStatsInfo.TotalCount);
            response.WithEtag(queryStatsInfo.ETag);
        }

        public static void WithDeterministicEtag(this HttpResponse response, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            var guid = DeterministicGuid.MakeId(data);
            response.WithEtag(guid.ToString());
        }

        static void WithHeader(this HttpResponse response, string header, StringValues value) => response.Headers.Append(header, value);

        public static void WithPagingLinks(this HttpResponse response, PagingInfo pageInfo, long highestTotalCountOfAllInstances, long totalResults)
        {
            if (totalResults <= PagingInfo.DefaultPageSize)
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
            response.WithDeterministicEtag(queryStats.ETag);
        }

        public static void WithPagingLinksAndTotalCount(this HttpResponse response,
            PagingInfo pagingInfo, long totalCount, long highestTotalCountOfAllInstances = 1)
        {
            response.WithTotalCount(totalCount);
            response.WithPagingLinks(pagingInfo, highestTotalCountOfAllInstances, totalCount);
        }
    }
}