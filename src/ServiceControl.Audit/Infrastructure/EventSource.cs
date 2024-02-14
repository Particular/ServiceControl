namespace ServiceControl.Audit.Infrastructure
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    class EventSource
    {
        public static void Create()
        {
            // TODO: Figure a way to achieve something but in the linux way
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }

        public const string SourceName = "ServiceControl.Audit";
    }
}