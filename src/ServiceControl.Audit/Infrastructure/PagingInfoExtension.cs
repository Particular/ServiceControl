namespace ServiceControl.Audit.Infrastructure
{
    using System.Net.Http;

    public static class PagingInfoExtension
    {
        public static PagingInfo GetPagingInfo(this HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", PagingInfo.DefaultPageSize);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = PagingInfo.DefaultPageSize;
            }

            var page = request.GetQueryStringValue("page", 1);
            if (page < 1)
            {
                page = 1;
            }

            return new PagingInfo(page, maxResultsPerPage);
        }
    }
}
