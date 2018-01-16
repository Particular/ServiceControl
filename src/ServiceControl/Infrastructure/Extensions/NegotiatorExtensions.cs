namespace ServiceBus.Management.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using global::Nancy;
    using global::Nancy.Helpers;
    using global::Nancy.Responses.Negotiation;
    using ServiceControl.Infrastructure;

    public static class NegotiatorExtensions
    {
        public static Negotiator WithPagingLinksAndTotalCount(this Negotiator negotiator, RavenQueryStatistics stats,
            Request request)

        {
            return negotiator.WithPagingLinksAndTotalCount(stats.TotalResults, request);
        }

        public static Negotiator WithPagingLinksAndTotalCount(this Negotiator negotiator, string sort, string direction, string browseDirection, int pageSize, IEnumerable<int> totalCounts, int[] offsetValues, int[] newOffsetValues, Request request)
        {
            int pageNumber = request.Query.page.HasValue
                ? int.Parse(request.Query.page)
                : 1;

            var newOffsets = string.Join(",", newOffsetValues);
            var totalCount = totalCounts.Sum();

            var lastPageOffsets = totalCounts.Select(x => x - 1);
            var pageCount = totalCount / pageSize + 1;

            var links = new List<string>();
            if (pageNumber > 1)
            {
                links.Add(GenerateLink(request, "prev", pageSize, browseDirection == "left" ? newOffsets : string.Join(",", offsetValues.Select(o => o - 1)), direction, sort, "left", pageNumber - 1));
                links.Add(GenerateLink(request, "first", pageSize, "", direction, sort, "right", 1));
            }
            if (pageNumber < pageCount)
            {
                links.Add(GenerateLink(request, "next", pageSize, browseDirection == "right" ? newOffsets : string.Join(",", offsetValues.Select(o => o + 1)), direction, sort, "right", pageNumber + 1));
                links.Add(GenerateLink(request, "last", pageSize, string.Join(",", lastPageOffsets), direction, sort, "left", pageCount));
            }

            return negotiator
                .WithHeader("Link", string.Join(",", links))
                .WithHeader("Total-Count", totalCount.ToString())
                .WithHeader("Page-Size", pageSize.ToString())
                .WithHeader("Page-Number", pageNumber.ToString());
        }

        static string GenerateLink(Request request, string rel, int pageSize, string newOffsets, string direction, string sort, string browseDirection, int pageNumber)
        {
            return $"<{request.Path.TrimStart('/')}?per_page={pageSize}&offsets={newOffsets}&direction={direction}&sort={sort}&browse_direction={browseDirection}&page={pageNumber}>; rel=\"{rel}\"";
        }

        public static Negotiator WithPagingLinksAndTotalCount(this Negotiator negotiator, int totalCount,
            Request request)
        {
            return negotiator.WithTotalCount(totalCount)
                .WithPagingLinks(totalCount, request);
        }

        public static Negotiator WithTotalCount(this Negotiator negotiator, RavenQueryStatistics stats)
        {
            return negotiator.WithTotalCount(stats.TotalResults);
        }

        public static Negotiator WithTotalCount(this Negotiator negotiator, int total)
        {
            return negotiator.WithHeader("Total-Count", total.ToString(CultureInfo.InvariantCulture));
        }

        public static Negotiator WithPagingLinks(this Negotiator negotiator, RavenQueryStatistics stats, Request request)
        {
            return negotiator.WithPagingLinks(stats.TotalResults, request);
        }

        public static Negotiator WithPagingLinks(this Negotiator negotiator, int totalResults, Request request)
        {
            decimal maxResultsPerPage = 50;

            if (request.Query.per_page.HasValue)
            {
                maxResultsPerPage = request.Query.per_page;
            }

            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = 1;

            if (request.Query.page.HasValue)
            {
                page = request.Query.page;
            }

            if (page < 1)
            {
                page = 1;
            }

            // No need to add a Link header if no paging
            if (totalResults <= maxResultsPerPage)
            {
                return negotiator;
            }

            var links = new List<string>();
            var lastPage = (int) Math.Ceiling(totalResults / maxResultsPerPage);

            // No need to add a Link header if page does not exist!
            if (page > lastPage)
            {
                return negotiator;
            }

            var queryParts = HttpUtility.ParseQueryString(request.Url.Query);
            var url = request.Url.Clone();
            var query = new StringBuilder();

            query.Append("?");
            foreach (var key in queryParts.AllKeys.Where(key => key != "page"))
            {
                query.AppendFormat("{0}={1}&", key, queryParts[key]);
            }

            var queryParams = query.ToString();

            if (page != 1)
            {
                AddLink(links, 1, "first", queryParams, url);
            }

            if (page > 1)
            {
                AddLink(links, page - 1, "prev", queryParams, url);
            }

            if (page != lastPage)
            {
                AddLink(links, lastPage, "last", queryParams, url);
            }

            if (page < lastPage)
            {
                AddLink(links, page + 1, "next", queryParams, url);
            }

            return negotiator.WithHeader("Link", String.Join(", ", links));
        }

        static void AddLink(ICollection<string> links, int page, string rel, string queryParams, Url url)
        {
            url.Query = queryParams + "page=" + page;

            links.Add($"<{url}>; rel=\"{rel}\"");
        }

        public static Negotiator WithEtagAndLastModified(this Negotiator negotiator, QueryHeaderInformation stats)
        {
            var etag = stats.IndexEtag;
            var responseLastModified = stats.IndexTimestamp;

            return WithEtagAndLastModified(negotiator, etag, responseLastModified);
        }

        public static Negotiator WithEtagAndLastModified(this Negotiator negotiator, RavenQueryStatistics stats)
        {
            var etag = stats.IndexEtag;
            var responseLastModified = stats.IndexTimestamp;

            return WithEtagAndLastModified(negotiator, etag, responseLastModified);
        }

        public static Negotiator WithDeterministicEtag(this Negotiator negotiator, string data)
        {
            var guid = DeterministicGuid.MakeId(data);
            return negotiator
                .WithHeader("ETag", guid.ToString());
        }

        public static Negotiator WithEtagAndLastModified(this Negotiator negotiator, Etag etag, DateTime responseLastModified)
        {
            var currentEtag = etag?.ToString();
            if (currentEtag != null)
            {
                negotiator.WithHeader("ETag", currentEtag);
            }
            return negotiator
                .WithHeader("Last-Modified", responseLastModified.ToString("R"));
        }

        public static Negotiator WithLastModified(this Negotiator negotiator, DateTime responseLastModified)
        {
            return negotiator
                .WithHeader("Last-Modified", responseLastModified.ToString("R"));
        }
    }
}