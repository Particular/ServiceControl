namespace ServiceControl.HeartbeatMonitoring
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class HeartbeatsIndex : AbstractIndexCreationTask<Heartbeat>
    {
        public HeartbeatsIndex()
        {
            Map = docs => from heartbeat in docs
                select new Heartbeat
                       {
                           ReportedStatus = heartbeat.ReportedStatus,
                       };

            DisableInMemoryIndexing = true;
        }
    }
}
