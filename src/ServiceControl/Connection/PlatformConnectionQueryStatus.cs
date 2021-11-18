namespace ServiceControl.Connection
{
    using System.Collections.Concurrent;

    public class PlatformConnectionQueryStatus
    {
        public bool IsSuccess { get; set; }
        public ConcurrentBag<string> Exceptions { get; set; } = new ConcurrentBag<string>();
    }
}