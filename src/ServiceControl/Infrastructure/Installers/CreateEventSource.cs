namespace ServiceBus.Management.Infrastructure.Installers
{
    using System.Diagnostics;
    using NServiceBus.Installation;
    using NServiceBus.Installation.Environments;

    public class CreateEventSource : INeedToInstallSomething<Windows>
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