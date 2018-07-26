namespace ServiceControl.LoadTests.Reporter
{
    using System.Threading;

    class Statistics
    {
        long audits;

        public string HostId { get; private set; }
        public long Audits => Interlocked.Read(ref audits);
        
        public void AuditReceived(string hostId)
        {
            if (hostId != HostId)
            {
                HostId = hostId;
                Interlocked.Exchange(ref audits, 0); //Reset the stats
            }
            else
            {
                Interlocked.Increment(ref audits);
            }
        }
    }
}