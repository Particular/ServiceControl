namespace ServiceControl.Infrastructure
{
    using System.Net.Http;
    using Extensions;

    class PagingInfo
    {
        public int Page { get; }
        public int PageSize { get; }
        public int Offset { get; }
        public int Next { get; }

        public PagingInfo(int page, int pageSize)
        {
            Page = page;
            PageSize = pageSize;
            Next = pageSize;
            Offset = (Page - 1) * Next;
        }
    }

    static class PagingInfoExtension
    {
        public static PagingInfo GetPagingInfo(this HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
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