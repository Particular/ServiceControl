namespace ServiceControl.Audit.Infrastructure
{
    public class PagingInfo
    {
        public const int DefaultPageSize = 50;

        public int Page { get; }
        public int PageSize { get; }
        public int Offset { get; }
        public int Next { get; }

        public PagingInfo(int page = 1, int pageSize = DefaultPageSize)
        {
            Page = page;
            PageSize = pageSize;
            Next = pageSize;
            Offset = (Page - 1) * Next;
        }
    }
}