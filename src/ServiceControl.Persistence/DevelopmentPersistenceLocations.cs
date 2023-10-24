namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.IO;

    static class DevelopmentPersistenceLocations
    {
        public static List<string> ManifestFiles { get; } = new List<string>();

        static DevelopmentPersistenceLocations()
        {
            var assembly = typeof(DevelopmentPersistenceLocations).Assembly.Location;
            var assemblyDirectory = Path.GetDirectoryName(assembly);

            // Becomes null if it navigates past the root of a drive
            var srcFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyDirectory))));

            if (!string.IsNullOrWhiteSpace(srcFolder) && srcFolder.EndsWith("src"))
            {
                ManifestFiles.Add(BuildManifestPath(srcFolder, "ServiceControl.Persistence.RavenDb5"));
            }
        }

        static string BuildManifestPath(string srcFolder, string projectName) => Path.Combine(srcFolder, projectName, "bin", configuration, framework, "persistence.manifest");

#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif

#if NET472
        const string framework = "net472";
#endif
    }
}
