namespace ServiceControl.Persistence.Infrastructure
{
    using System.Diagnostics;

    [DebuggerDisplay("{Sort} {Direction}")]
    public class SortInfo(string sort, string direction = "desc")
    {
        public string Direction { get; } = direction;
        public string Sort { get; } = sort;
    }
}