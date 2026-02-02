namespace ServiceControl.Audit.Persistence
{
    using System.Collections.Generic;
    using System.IO;

    static class DevelopmentPersistenceLocations
    {
        public static List<string> ManifestFiles { get; } = [];

        static DevelopmentPersistenceLocations()
        {
            var assembly = typeof(DevelopmentPersistenceLocations).Assembly.Location;
            var assemblyDirectory = Path.GetDirectoryName(assembly);

            // Becomes null if it navigates past the root of a drive
            var srcFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyDirectory))));

            if (!string.IsNullOrWhiteSpace(srcFolder) && srcFolder.EndsWith("src"))
            {
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Audit.Persistence.InMemory"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Audit.Persistence.RavenDB"));
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Audit.Persistence.MongoDB"));
            }
        }

        static string BuildManifestPath(string srcFolder, string projectName) => Path.Combine(srcFolder, projectName, "bin", configuration, framework, "persistence.manifest");

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif

#if NET10_0
        const string framework = "net10.0";
#endif
    }
}
