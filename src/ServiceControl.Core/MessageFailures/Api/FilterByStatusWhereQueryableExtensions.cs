namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Nancy;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    public static class FilterByStatusWhereQueryableExtensions
    {
        public static IDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions> FilterByStatusWhere(this IDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions> source, Request request)
        {
            string status = null;

            if ((bool)request.Query.status.HasValue)
            {
                status = (string)request.Query.status;
            }

            if (status == null)
            {
                return source;
            }

            var filters = status.Replace(" ", String.Empty).Split(',');
            var excludes = new List<int>();
            var includes = new List<int>();

            foreach (var filter in filters)
            {
                FailedMessageStatus failedMessageStatus;

                if (filter.StartsWith("-"))
                {
                    if (Enum.TryParse(filter.Substring(1), true, out failedMessageStatus))
                    {
                        excludes.Add((int)failedMessageStatus);
                    }
                    continue;
                }

                if (Enum.TryParse(filter, true, out failedMessageStatus))
                {
                    includes.Add((int)failedMessageStatus);
                }
            }

            var sb = new StringBuilder();

            sb.Append("((");
            if (includes.Count == 0)
            {
                sb.Append("*");
            }
            else
            {
                sb.Append(String.Join(" OR ", includes.ToArray()));
            }
            sb.Append(")");

            if (excludes.Count > 0)
            {
                sb.Append(" AND NOT (");
                sb.Append(String.Join(" OR ", excludes.ToArray()));
                sb.Append(")");
            }
            sb.Append(")");

            source.Where(string.Format("Status: {0}", sb));

            return source;
        }
    }
}