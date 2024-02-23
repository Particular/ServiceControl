namespace Particular.ThroughputCollector.Exceptions
{
    using System;

    class ServiceControlDataException : Exception
    {
        public string Url { get; }
        public int Attempts { get; }

        public ServiceControlDataException(string url, int tryCount, Exception inner)
            : base(inner.Message + $" (Attempted operation {tryCount} total times)", inner)
        {
            Url = url;
            Attempts = tryCount;
        }
    }
}