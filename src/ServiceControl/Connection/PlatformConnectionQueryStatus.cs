namespace ServiceControl.Connection
{
    using System.Collections.Concurrent;

    public class PlatformConnectionQueryStatus
    {
        public ConcurrentBag<string> Exceptions { get; set; } = new ConcurrentBag<string>();
    }
}