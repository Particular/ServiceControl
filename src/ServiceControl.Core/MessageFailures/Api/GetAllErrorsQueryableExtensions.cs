namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Nancy;
    using Raven.Client;

    public static class GetAllErrorsQueryableExtensions
    {
        public static IDocumentQuery<TSource> Paging<TSource>(this IDocumentQuery<TSource> source, Request request)
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

            var skipResults = (page - 1)*maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IDocumentQuery<TSource> Sort<TSource>(this IDocumentQuery<TSource> source, Request request)
        {
            var direction = "desc";
            var descending = true;

            if (request.Query.direction.HasValue)
            {
                direction = (string) request.Query.direction;
            }

            if (direction == "asc")
            {
                @descending = false;
            }

            var sortOptions = new[]
            {
                "id",
                "message_id",
                "message_type",
                "time_sent",
                "status"
            };

            var sort = "time_sent";
            string keySelector;

            if (request.Query.sort.HasValue)
            {
                sort = (string) request.Query.sort;
            }

            if (!sortOptions.Contains(sort))
            {
                sort = "time_sent";
            }

            switch (sort)
            {
                case "id":
                case "message_id":
                    keySelector = "MessageId";
                    break;

                case "message_type":
                    keySelector = "MessageType";
                    break;

                case "status":
                    keySelector = "Status";
                    break;

                default:
                    keySelector = "TimeSent";
                    break;
            }

            return source.AddOrder(keySelector, @descending);
        }
    }
}