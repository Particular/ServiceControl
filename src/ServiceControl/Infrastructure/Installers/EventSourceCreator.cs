namespace ServiceBus.Management.Infrastructure.Installers
{
    using System.Diagnostics;

    public static class EventSourceCreator
    {
        public static void Create()
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }

        public const string SourceName = "ServiceControl";
    }
}