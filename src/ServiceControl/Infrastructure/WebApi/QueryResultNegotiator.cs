namespace ServiceControl.Infrastructure.WebApi
{
    using System.Net;
    using System.Net.Http;
    using QueryResult = CompositeViews.Messages.QueryResult;

    static class QueryResultNegotiator
    {
        public static HttpResponseMessage FromQueryResult(HttpRequestMessage request, QueryResult queryResult, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var response = request.CreateResponse(statusCode, queryResult.DynamicResults);
            var queryStats = queryResult.QueryStats;

            return response.WithPagingLinksAndTotalCount(queryStats.TotalCount, queryStats.HighestTotalCountOfAllTheInstances, request)
                .WithDeterministicEtag(queryStats.ETag);
        }
    }
}