namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    static class DevelopmentTransportLocations
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif

#if NET472
        const string framework = "net472";
#endif

        public static bool TryGetTransportFolder(string transportName, out string transportFolder)
        {
            transportFolder = null;

            if (transportName.Contains("."))
            {
                transportName = transportName.Split('.')[0];
            }

            var found = projects.TryGetValue(transportName, out var projectFolder);

            if (found)
            {
                var assemblyPath = typeof(DevelopmentTransportLocations).Assembly.Location;
                var assemblyFolder = Path.GetDirectoryName(assemblyPath);
                var srcFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyFolder))))));
                transportFolder = Path.Combine(srcFolder, projectFolder, "bin", configuration, framework);
            }

            return found;
        }

        static readonly Dictionary<string, string> projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"AmazonSQS", "ServiceControl.Transports.SQS" },
            {"AzureServiceBus", "ServiceControl.Transports.ASB" },
            {"AzureStorageQueue", "ServiceControl.Transports.ASQ" },
            {"LearningTransport", "ServiceControl.Transports.Learning" },
            {"MSMQ", "ServiceControl.Transports.Msmq" },
            {"NetStandardAzureServiceBus", "ServiceControl.Transports.ASBS" },
            {"RabbitMQ", "ServiceControl.Transports.RabbitMQ" },
            {"SQLServer", "ServiceControl.Transports.SqlServer" }
        };
    }
}
