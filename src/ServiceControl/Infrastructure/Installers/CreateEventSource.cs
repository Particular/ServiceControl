namespace ServiceBus.Management.Infrastructure.Installers
{
    using System.Diagnostics;
    using NServiceBus.Installation;

    public class CreateEventSource : INeedToInstallSomething
    {
        public const string SourceName = "ServiceControl";

        public void Install(string identity)
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }
        }
    }
}