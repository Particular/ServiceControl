namespace ServiceBus.Management.Extensions
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Nancy;

    public static class QueryableExtensions
    {
        public static IQueryable<TSource> Paging<TSource>(this IQueryable<TSource> source, Request request)
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

        public static IOrderedQueryable<Message> Sort(this IQueryable<Message> source, Request request)
        {
            var direction = "desc";

            if (request.Query.direction.HasValue)
            {
                direction = (string)request.Query.direction;
            }

            if (direction != "asc" && direction != "desc")
            {
                direction = "desc";
            }

            var sortOptions = new [] {"time_of_failure", "id", "message_type", "time_sent"};
            var sort = "time_of_failure";
            Expression<Func<Message, object>> keySelector;

            if (request.Query.sort.HasValue)
            {
                sort = (string)request.Query.sort;
            }

            if (!sortOptions.Contains(sort))
            {
                sort = "time_of_failure"; 
            }

            switch (sort)
            {
                case "id":
                    keySelector = m => m.Id;
                    break;

                case "message_type":
                    keySelector = m => m.MessageType;
                    break;

                case "time_sent":
                    keySelector = m => m.TimeSent;
                    break;

                default:
                    keySelector = m => m.FailureDetails.TimeOfFailure;
                    break;
            }

            if (direction == "asc")
            {
                return source.OrderBy(keySelector);
            }

            return source.OrderByDescending(keySelector);
        }
    }
}