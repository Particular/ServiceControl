namespace ServiceControl.Transports
{
    using System.Collections.Generic;
    using System.IO;

    static class DevelopmentTransportLocations
    {
        public static List<string> ManifestFiles { get; } = new List<string>();

        static DevelopmentTransportLocations()
        {
            var assembly = typeof(DevelopmentTransportLocations).Assembly.Location;
            var assemblyDirectory = Path.GetDirectoryName(assembly);

            // Becomes null if it navigates past the root of a drive
            var srcFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyDirectory))));

            if (!string.IsNullOrWhiteSpace(srcFolder) && srcFolder.EndsWith("src"))
            {
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Transports.ASBS"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Transports.ASQ"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Transports.Learning"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Transports.Msmq"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Transports.RabbitMQ"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Transports.SqlServer"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Transports.SQS"));
            }
        }

        static string BuildManifestPath(string srcFolder, string projectName) => Path.Combine(srcFolder, projectName, "bin", configuration, framework, "transport.manifest");

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif

#if NET48
        const string framework = "net472";
#endif
    }
}
