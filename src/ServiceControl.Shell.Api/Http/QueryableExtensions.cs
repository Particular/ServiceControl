namespace ServiceControl.Infrastructure.Extensions
{
    using System.Linq;
    using Nancy;
    using Raven.Client.Linq;

    public static class QueryableExtensions
    {
        public static IRavenQueryable<TSource> Paging<TSource>(this IRavenQueryable<TSource> source, Request request)
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

            return (IRavenQueryable<TSource>)source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }
    }
}