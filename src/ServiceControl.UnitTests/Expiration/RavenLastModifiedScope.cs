namespace ServiceControl.UnitTests.Expiration
{
    using System;
    using Raven.Abstractions;

    public class RavenLastModifiedScope : IDisposable
    {
        public RavenLastModifiedScope(DateTime dateTime)
        {
            previous = SystemTime.UtcDateTime;
            SystemTime.UtcDateTime = () => dateTime;
        }

        public void Dispose()
        {
            SystemTime.UtcDateTime = previous;
        }

        Func<DateTime> previous;
    }
}