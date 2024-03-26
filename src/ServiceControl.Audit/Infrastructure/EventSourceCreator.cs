namespace ServiceControl.Audit.Infrastructure
{
    using System.Diagnostics;
    using System.Runtime.Versioning;

    static class EventSourceCreator
    {
        [SupportedOSPlatform("windows")]
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