namespace ServiceControl.Monitoring
{
    using System.Threading;

    public class EndpointMonitoringStats
    {
        public int Active => active;
        public int Failing => failing;

        public void RecordActive() => Interlocked.Increment(ref active);
        public void RecordFailing() => Interlocked.Increment(ref failing);
        private int active;
        private int failing;
    }
}