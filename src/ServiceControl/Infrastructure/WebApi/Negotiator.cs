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
    using Raven.Abstractions.Data;
    using Raven.Client;
    using QueryResult = CompositeViews.Messages.QueryResult;

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
                maxResultsPerPage = Decimal.Parse(per_pageParameter);
            }

            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = 1;

            var pageParameter = queryNameValuePairs.LastOrDefault(x => x.Key == "page").Value;
            if (pageParameter != null)
            {
                page = Int32.Parse(pageParameter);
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

            var path = request.RequestUri.AbsolutePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) 
                ? request.RequestUri.AbsolutePath.Substring(5) // NOTE: Strips off the /api/ for backwards compat
                : request.RequestUri.AbsolutePath.TrimStart('/'); 
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

        public static HttpResponseMessage WithEtag(this HttpResponseMessage response, RavenQueryStatistics stats)
        {
            var etag = stats.IndexEtag;

            return response.WithEtag(etag);
        }

        public static HttpResponseMessage WithDeterministicEtag(this HttpResponseMessage response, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return response;
            }

            var guid = DeterministicGuid.MakeId(data);
            return response.WithEtag(Etag.Parse(guid.ToString()));
        }

        public static HttpResponseMessage WithEtag(this HttpResponseMessage response, Etag etag)
        {
            response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
            return response;
        }

        private static HttpResponseMessage WithHeader(this HttpResponseMessage response, string header, string value)
        {
            response.Headers.Add(header, value);
            return response;
        }
    }
}