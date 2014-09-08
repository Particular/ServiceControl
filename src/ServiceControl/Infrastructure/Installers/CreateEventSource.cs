namespace ServiceBus.Management.Infrastructure.Installers
{
    using System.Diagnostics;
    using NServiceBus;
    using NServiceBus.Installation;

    public class CreateEventSource : INeedToInstallSomething
    {
        public const string SourceName = "ServiceControl";

        public void Install(string identity, Configure config)
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }
    }
}