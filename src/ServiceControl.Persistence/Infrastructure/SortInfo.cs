namespace ServiceControl.Persistence.Infrastructure
{
    using System.Diagnostics;

    [DebuggerDisplay("{Sort} {Direction}")]
    public class SortInfo(string sort = null, string direction = null)
    {
        public string Direction { get; } = string.IsNullOrWhiteSpace(direction) ? "desc" : direction;
        public string Sort { get; } = string.IsNullOrWhiteSpace(sort) ? "time_sent" : sort;
    }
}