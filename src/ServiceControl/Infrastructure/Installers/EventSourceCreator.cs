namespace ServiceBus.Management.Infrastructure.Installers
{
    using System;
    using System.Diagnostics;
    using System.Runtime.Versioning;

    static class EventSourceCreator
    {
        [SupportedOSPlatform("windows")]
        public static void Create()
        {
            if (EventLog.SourceExists(SourceName))
            {
                return;
            }

            try
            {
                EventLog.CreateEventSource(SourceName, null);
            }
            // An event source is machine-wide, and the check above cannot be made atomic with the
            // create. Anything else running this at the same time, another instance being set up or,
            // in CI, another test assembly, can get there first. The filter keeps that case distinct
            // from an ArgumentException that means the source name itself is unusable.
            catch (ArgumentException) when (EventLog.SourceExists(SourceName))
            {
            }
        }

        public const string SourceName = "ServiceControl";
    }
}