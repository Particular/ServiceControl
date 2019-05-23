﻿namespace ServiceBus.Management.Infrastructure.Installers
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using NServiceBus.Installation;

    public class CreateEventSource : INeedToInstallSomething
    {
        public Task Install(string identity)
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(SourceName, null);
            }

            return Task.FromResult(0);
        }

        public const string SourceName = "ServiceControl";
    }
}