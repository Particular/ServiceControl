namespace ServiceControl.Audit.Infrastructure
{
    using System.Diagnostics;

    class EventSource
    {
        public static void Create()
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }

        public const string SourceName = "ServiceControl.Audit";
    }
}
