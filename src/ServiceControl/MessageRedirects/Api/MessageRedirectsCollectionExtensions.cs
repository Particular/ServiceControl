namespace ServiceControl.MessageRedirects.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using Nancy;

    static class MessageRedirectsCollectionExtensions
    {
        public static IOrderedEnumerable<MessageRedirect> Sort(this MessageRedirectsCollection source, Request request, string defaultSortDirection = "desc")
        {
            var direction = defaultSortDirection;

            if (request.Query.direction.HasValue)
            {
                direction = (string) request.Query.direction;
            }

            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }

            var sortOptions = new[]
            {
                "from_physical_address",
                "to_physical_address"
            };

            var sort = "from_physical_address";

            if (request.Query.sort.HasValue)
            {
                sort = (string) request.Query.sort;
            }

            if (!sortOptions.Contains(sort))
            {
                sort = "from_physical_address";
            }

            if (sort == "to_physical_address")
            {
                return direction == "asc" ? source.Redirects.OrderBy(r => r.ToPhysicalAddress) : source.Redirects.OrderByDescending(r => r.ToPhysicalAddress);
            }

            return direction == "asc" ? source.Redirects.OrderBy(r => r.FromPhysicalAddress) : source.Redirects.OrderByDescending(r => r.FromPhysicalAddress);

        }

        public static IEnumerable<MessageRedirect> Paging(this IEnumerable<MessageRedirect> source, Request request)
        {
            var maxResultsPerPage = 50;

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

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }
    }
}