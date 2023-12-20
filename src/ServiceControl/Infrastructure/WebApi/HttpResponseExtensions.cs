namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Extensions.Primitives;
    using ServiceControl.Persistence.Infrastructure;

    static class HttpResponseExtensions
    {
        public static void WithTotalCount(this HttpResponse response, int totalCount) => response.WithHeader("Total-Count", totalCount.ToString(CultureInfo.InvariantCulture));

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

        public static void WithPagingLinks(this HttpResponse response, PagingInfo pageInfo, int highestTotalCountOfAllInstances, int totalResults)
        {
            if (totalResults <= PagingInfo.DefaultPageSize)
            {
                return;
            }

            var links = new List<string>();
            var lastPage = (int)Math.Ceiling((double)highestTotalCountOfAllInstances / pageInfo.PageSize);

            // No need to add a Link header if page does not exist!
            if (pageInfo.Page > lastPage)
            {
                return;
            }

            var path = Uri.UnescapeDataString(response.HttpContext.Request.GetEncodedPathAndQuery())
                .Replace("/api/", string.Empty); // NOTE: Strips off the /api/ for backwards compat
            var query = new StringBuilder();

            query.Append("?");
            foreach (var pair in response.HttpContext.Request.Query.Where(pair => pair.Key != "page"))
            {
                query.AppendFormat("{0}={1}&", pair.Key, pair.Value);
            }

            var queryParams = query.ToString();

            if (pageInfo.Page != 1)
            {
                AddLink(links, 1, "first", path, queryParams);
            }

            if (pageInfo.Page > 1)
            {
                AddLink(links, pageInfo.Page - 1, "prev", path, queryParams);
            }

            if (pageInfo.Page != lastPage)
            {
                AddLink(links, lastPage, "last", path, queryParams);
            }

            if (pageInfo.Page < lastPage)
            {
                AddLink(links, pageInfo.Page + 1, "next", path, queryParams);
            }

            // TODO can this be new StringValues(links.ToArray())) ? we don't know what the separator will be
            // https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Primitives/src/StringValues.cs#L235
            response.WithHeader("Link", new StringValues(string.Join(", ", links)));
        }

        static void AddLink(ICollection<string> links, int page, string rel, string uriPath, string queryParams)
        {
            var query = $"{queryParams}page={page}";

            links.Add($"<{uriPath + query}>; rel=\"{rel}\"");
        }

        // TODO This name might need to change to better reflect what it does
        public static void WithQueryResults(this HttpResponse response, QueryStatsInfo queryStats, PagingInfo pagingInfo)
        {
            response.WithPagingLinksAndTotalCount(pagingInfo, queryStats.TotalCount, queryStats.HighestTotalCountOfAllTheInstances);
            response.WithDeterministicEtag(queryStats.ETag);
        }

        public static void WithPagingLinksAndTotalCount(this HttpResponse response,
            PagingInfo pagingInfo, int totalCount, int highestTotalCountOfAllInstances = 1)
        {
            response.WithTotalCount(totalCount);
            response.WithPagingLinks(pagingInfo, highestTotalCountOfAllInstances, totalCount);
        }
    }
}