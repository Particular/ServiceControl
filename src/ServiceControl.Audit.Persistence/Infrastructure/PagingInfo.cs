namespace ServiceControl.Audit.Infrastructure
{
    using System.Diagnostics;

    [DebuggerDisplay("{Page}/{PageSize}")]
    public class PagingInfo
    {
        public const int DefaultPageSize = 50;

        public int Page { get; }
        public int PageSize { get; }
        public int Offset { get; }
        public int Next { get; }

        public PagingInfo(int? page = null, int? pageSize = null)
        {
            Page = page ?? 1;
            PageSize = pageSize ?? DefaultPageSize;
            Next = PageSize;
            Offset = (Page - 1) * Next;
        }
    }
}