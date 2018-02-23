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
    using QueryResult = ServiceControl.CompositeViews.Messages.QueryResult;

    public static class NegotiatorExtensions
    {
        public static Negotiator WithPagingLinksAndTotalCount(this Negotiator negotiator, RavenQueryStatistics stats,
            Request request)

        {
            return negotiator.WithPagingLinksAndTotalCount(stats.TotalResults, request);
        }

        public static Negotiator WithPagingLinksAndTotalCount(this Negotiator negotiator, int totalCount, int highestTotalCountOfAllInstances,
            Request request)
        {
            return negotiator.WithTotalCount(totalCount)
                .WithPagingLinks(totalCount, highestTotalCountOfAllInstances, request);
        }

        public static Negotiator WithQueryResult(this Negotiator negotiator, QueryResult queryResult, Request request)
        {
            var queryStats = queryResult.QueryStats;

            return negotiator.WithModel(queryResult.DynamicResults)
                .WithPagingLinksAndTotalCount(queryStats.TotalCount, queryStats.HighestTotalCountOfAllTheInstances, request)
                .WithDeterministicEtag(queryStats.ETag);
        }

        public static Negotiator WithPagingLinksAndTotalCount(this Negotiator negotiator, int totalCount,
            Request request)
        {
            return negotiator.WithTotalCount(totalCount)
                .WithPagingLinks(totalCount, request);
        }

        public static Negotiator WithTotalCount(this Negotiator negotiator, int total)
        {
            return negotiator.WithHeader("Total-Count", total.ToString(CultureInfo.InvariantCulture));
        }

        public static Negotiator WithPagingLinks(this Negotiator negotiator, int totalResults, Request request)
        {
            return negotiator.WithPagingLinks(totalResults, 1, request);
        }

        public static Negotiator WithPagingLinks(this Negotiator negotiator, int totalResults, int highestTotalCountOfAllInstances, Request request)
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
            var lastPage = (int) Math.Ceiling(highestTotalCountOfAllInstances / maxResultsPerPage);

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

        public static Negotiator WithEtag(this Negotiator negotiator, RavenQueryStatistics stats)
        {
            var etag = stats.IndexEtag;

            return WithEtag(negotiator, etag);
        }

        public static Negotiator WithDeterministicEtag(this Negotiator negotiator, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return negotiator;
            }

            var guid = DeterministicGuid.MakeId(data);
            return negotiator
                .WithHeader("ETag", guid.ToString());
        }

        public static Negotiator WithEtag(this Negotiator negotiator, string etag)
        {
            if (etag != null)
            {
                negotiator.WithHeader("ETag", etag);
            }

            return negotiator;
        }

        public static Negotiator WithEtag(this Negotiator negotiator, Etag etag)
        {
            return negotiator.WithEtag(etag?.ToString());
        }
    }
}