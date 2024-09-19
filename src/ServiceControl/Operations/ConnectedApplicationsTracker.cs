namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using ServiceBus.Management.Infrastructure.Settings;

    class ConnectedApplicationsTracker(Settings settings)
    {
        public const string ConnectedApplicationControlHeader = "ServiceControl.Connectors.Application";

        public void RecordConnectedApplication(string application)
        {
            if (AlreadyAddedApplications.Add(application))
            {
                settings.ConnectedApplications.Add(application);
            }
        }

        HashSet<string> AlreadyAddedApplications { get; init; } = [];
    }
}
