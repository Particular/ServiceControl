namespace ServiceBus.Management.Infrastructure.Installers
{
    using System.Runtime.InteropServices;
    using System.Diagnostics;

    public static class EventSourceCreator
    {
        public static void Create()
        {
            // TODO: Figure a way to achieve something but in the linux way
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }

        public const string SourceName = "ServiceControl";
    }
}