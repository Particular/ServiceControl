using System.Runtime.InteropServices;

namespace ServiceBus.Management.Infrastructure.Installers
{
    using System.Diagnostics;

    public static class EventSourceCreator
    {
        public static void Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }

        public const string SourceName = "ServiceControl";
    }
}