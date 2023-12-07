namespace ServiceControl.Audit.Infrastructure
{
    public class SortInfo
    {
        public string Direction { get; }
        public string Sort { get; }

        public SortInfo(string sort, string direction)
        {
            Sort = sort;
            Direction = direction;
        }
    }
}