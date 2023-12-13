namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using ServiceControl.Persistence.Infrastructure;
    using QueryResult = Persistence.Infrastructure.QueryResult;

    static class RequestExtensions
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

        static void WithPagingLinks(this HttpResponse response, PagingInfo pageInfo, int highestTotalCountOfAllInstances, int totalResults)
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

            var path = response.HttpContext.Request.GetEncodedUrl().Substring(5); // NOTE: Strips off the /api/ for backwards compat
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

    static class Negotiator
    {
        public static HttpResponseMessage FromModel(HttpRequestMessage request, object value, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var response = request.CreateResponse(statusCode, value);

            return response;
        }

        public static HttpResponseMessage FromQueryResult(HttpRequestMessage request, QueryResult queryResult, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var response = request.CreateResponse(statusCode, queryResult.DynamicResults);
            var queryStats = queryResult.QueryStats;

            return response.WithPagingLinksAndTotalCount(queryStats.TotalCount, queryStats.HighestTotalCountOfAllTheInstances, request)
                .WithDeterministicEtag(queryStats.ETag);
        }

        public static HttpResponseMessage WithReasonPhrase(this HttpResponseMessage response, string reasonPhrase)
        {
            response.ReasonPhrase = reasonPhrase;
            return response;
        }

        public static HttpResponseMessage WithPagingLinksAndTotalCount(this HttpResponseMessage response, int totalCount, int highestTotalCountOfAllInstances,
            HttpRequestMessage request)
        {
            return response.WithTotalCount(totalCount)
                .WithPagingLinks(totalCount, highestTotalCountOfAllInstances, request);
        }

        public static HttpResponseMessage WithPagingLinksAndTotalCount(this HttpResponseMessage response, int totalCount,
            HttpRequestMessage request)
        {
            return response.WithTotalCount(totalCount)
                .WithPagingLinks(totalCount, request);
        }

        public static HttpResponseMessage WithTotalCount(this HttpResponseMessage response, int total)
        {
            return response.WithHeader("Total-Count", total.ToString(CultureInfo.InvariantCulture));
        }

        public static HttpResponseMessage WithPagingLinks(this HttpResponseMessage response, int totalResults, HttpRequestMessage request)
        {
            return response.WithPagingLinks(totalResults, 1, request);
        }

        public static HttpResponseMessage WithPagingLinks(this HttpResponseMessage response, int totalResults, int highestTotalCountOfAllInstances, HttpRequestMessage request)
        {
            decimal maxResultsPerPage = 50;

            var queryNameValuePairs = request.GetQueryNameValuePairs().ToList();

            var per_pageParameter = queryNameValuePairs.LastOrDefault(x => x.Key == "per_page").Value;
            if (per_pageParameter != null)
            {
                maxResultsPerPage = decimal.Parse(per_pageParameter);
            }

            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = 1;

            var pageParameter = queryNameValuePairs.LastOrDefault(x => x.Key == "page").Value;
            if (pageParameter != null)
            {
                page = int.Parse(pageParameter);
            }

            if (page < 1)
            {
                page = 1;
            }

            // No need to add a Link header if no paging
            if (totalResults <= maxResultsPerPage)
            {
                return response;
            }

            var links = new List<string>();
            var lastPage = (int)Math.Ceiling(highestTotalCountOfAllInstances / maxResultsPerPage);

            // No need to add a Link header if page does not exist!
            if (page > lastPage)
            {
                return response;
            }

            var path = request.RequestUri.AbsolutePath.Substring(5); // NOTE: Strips off the /api/ for backwards compat
            var query = new StringBuilder();

            query.Append("?");
            foreach (var pair in queryNameValuePairs.Where(pair => pair.Key != "page"))
            {
                query.AppendFormat("{0}={1}&", pair.Key, pair.Value);
            }

            var queryParams = query.ToString();

            if (page != 1)
            {
                AddLink(links, 1, "first", path, queryParams);
            }

            if (page > 1)
            {
                AddLink(links, page - 1, "prev", path, queryParams);
            }

            if (page != lastPage)
            {
                AddLink(links, lastPage, "last", path, queryParams);
            }

            if (page < lastPage)
            {
                AddLink(links, page + 1, "next", path, queryParams);
            }

            return response.WithHeader("Link", string.Join(", ", links));
        }

        static void AddLink(ICollection<string> links, int page, string rel, string uriPath, string queryParams)
        {
            var query = $"{queryParams}page={page}";

            links.Add($"<{uriPath + query}>; rel=\"{rel}\"");
        }

        public static HttpResponseMessage WithEtag(this HttpResponseMessage response, string etag)
        {
            response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
            return response;
        }

        public static HttpResponseMessage WithDeterministicEtag(this HttpResponseMessage response, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return response;
            }

            var guid = DeterministicGuid.MakeId(data);
            return response.WithEtag(guid.ToString());
        }

        public static HttpResponseMessage FromQueryStatsInfo(HttpRequestMessage request, QueryStatsInfo queryStatsInfo)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            return WithTotalCount(response, queryStatsInfo.TotalCount)
                .WithEtag(queryStatsInfo.ETag);
        }

        static HttpResponseMessage WithHeader(this HttpResponseMessage response, string header, string value)
        {
            response.Headers.Add(header, value);
            return response;
        }
    }
}