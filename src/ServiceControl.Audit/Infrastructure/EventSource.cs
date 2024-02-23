namespace ServiceControl.Audit.Infrastructure
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    class EventSource
    {
        public static void Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }

        public const string SourceName = "ServiceControl.Audit";
    }
}