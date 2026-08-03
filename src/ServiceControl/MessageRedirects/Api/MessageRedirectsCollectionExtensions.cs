namespace ServiceControl.MessageRedirects.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Persistence.MessageRedirects;

    static class MessageRedirectsSortingExtensions
    {
        public static IOrderedEnumerable<MessageRedirect> Sort(this IReadOnlyList<MessageRedirect> source, string sort, string direction, string defaultSortDirection = "desc")
        {
            if (string.IsNullOrWhiteSpace(direction))
            {
                direction = defaultSortDirection;
            }

            if (direction is not "asc" and not "desc")
            {
                direction = defaultSortDirection;
            }

            if (string.IsNullOrWhiteSpace(sort))
            {
                sort = "from_physical_address";
            }

            if (!SortOptions.Contains(sort))
            {
                sort = "from_physical_address";
            }

            if (sort == "to_physical_address")
            {
                return direction == "asc" ? source.OrderBy(r => r.ToPhysicalAddress) : source.OrderByDescending(r => r.ToPhysicalAddress);
            }

            return direction == "asc" ? source.OrderBy(r => r.FromPhysicalAddress) : source.OrderByDescending(r => r.FromPhysicalAddress);
        }

        public static IEnumerable<MessageRedirect> Paging(this IEnumerable<MessageRedirect> source, PagingInfo pagingInfo) => source.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize);

        static HashSet<string> SortOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "from_physical_address",
            "to_physical_address"
        };
    }
}