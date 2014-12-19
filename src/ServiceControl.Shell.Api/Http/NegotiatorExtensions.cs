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

    public static class NegotiatorExtensions
    {
        public static Negotiator WithPagingLinksAndTotalCount(this Negotiator negotiator, RavenQueryStatistics stats,
            Request request)
        {
            return negotiator.WithTotalCount(stats)
                .WithPagingLinks(stats, request);
        }

        public static Negotiator WithTotalCount(this Negotiator negotiator, RavenQueryStatistics stats)
        {
            return negotiator.WithHeader("Total-Count", stats.TotalResults.ToString(CultureInfo.InvariantCulture));
        }

        public static Negotiator WithPagingLinks(this Negotiator negotiator, RavenQueryStatistics stats, Request request)
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
            if (stats.TotalResults <= maxResultsPerPage)
            {
                return negotiator;
            }

            var links = new List<string>();
            var lastPage = (int) Math.Ceiling(stats.TotalResults/maxResultsPerPage);

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

            links.Add(String.Format("<{0}>; rel=\"{1}\"", url, rel));
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

        public static Negotiator WithEtagAndLastModified(this Negotiator negotiator, Etag etag, DateTime responseLastModified)
        {
            var currentEtag = etag.ToString();
            return negotiator
                .WithHeader("ETag", currentEtag)
                .WithHeader("Last-Modified", responseLastModified.ToString("R"));
        }

        public static Negotiator WithRavenQueryStats(this Negotiator negotiator, RavenQueryStatistics stats)
        {
            return negotiator
                .WithHeader("QueryTimeMs", stats.DurationMilliseconds.ToString(CultureInfo.InvariantCulture))
                .WithHeader("IsStale", stats.IsStale.ToString());
        }
    }
}