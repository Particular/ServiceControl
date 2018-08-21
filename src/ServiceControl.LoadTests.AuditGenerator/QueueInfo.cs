namespace ServiceControl.LoadTests.AuditGenerator
{
    using System.Threading;

    class QueueInfo
    {
        long processed;
        long sent;

        public void Processed(long newProcessed)
        {
            Interlocked.Exchange(ref processed, newProcessed);
        }

        public void Sent()
        {
            Interlocked.Increment(ref sent);
        }

        public long Length => Interlocked.Read(ref sent) - Interlocked.Read(ref processed);
    }
}