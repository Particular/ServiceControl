namespace ServiceControl.Audit.Infrastructure
{
    using System.Diagnostics;
    using System.Threading.Tasks;

    class EventSource
    {
        public Task Create()
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }

            return Task.FromResult(0);
        }

        public const string SourceName = "ServiceControl.Audit";
    }
}
