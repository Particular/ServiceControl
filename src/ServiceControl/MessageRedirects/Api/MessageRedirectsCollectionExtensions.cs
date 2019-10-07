namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;

    static class MessageRedirectsCollectionExtensions
    {
        public static IOrderedEnumerable<MessageRedirect> Sort(this MessageRedirectsCollection source, HttpRequestMessage request, string defaultSortDirection = "desc")
        {
            var query = request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value);

            if (!query.TryGetValue("direction", out var direction))
            {
                direction = defaultSortDirection;
            }

            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }

            if (!query.TryGetValue("sort", out var sort))
            {
                sort = "from_physical_address";
            }

            if (!SortOptions.Contains(sort))
            {
                sort = "from_physical_address";
            }

            if (sort == "to_physical_address")
            {
                return direction == "asc" ? source.Redirects.OrderBy(r => r.ToPhysicalAddress) : source.Redirects.OrderByDescending(r => r.ToPhysicalAddress);
            }

            return direction == "asc" ? source.Redirects.OrderBy(r => r.FromPhysicalAddress) : source.Redirects.OrderByDescending(r => r.FromPhysicalAddress);
        }

        public static IEnumerable<MessageRedirect> Paging(this IEnumerable<MessageRedirect> source, HttpRequestMessage request)
        {
            var query = request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value);

            var maxResultsPerPage = 50;

            if (query.TryGetValue("per_page", out var query_per_page))
            {
                maxResultsPerPage = Convert.ToInt32(query_per_page);
            }

            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = 1;

            if (query.TryGetValue("page", out var query_page))
            {
                page = Convert.ToInt32(query_page);
            }

            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        static HashSet<string> SortOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "from_physical_address",
            "to_physical_address"
        };
    }
}