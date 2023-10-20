namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    static class DevelopmentPersistenceLocations
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif

#if NET472
        const string framework = "net472";
#endif

        public static bool TryGetPersistenceFolder(string transportName, out string persistenceFolder)
        {
            persistenceFolder = null;

            if (transportName.Contains("."))
            {
                transportName = transportName.Split('.')[0];
            }

            var found = projects.TryGetValue(transportName, out var projectFolder);

            if (found)
            {
                var assemblyPath = typeof(DevelopmentPersistenceLocations).Assembly.Location;
                var assemblyFolder = Path.GetDirectoryName(assemblyPath);
                var srcFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assemblyFolder))))));
                persistenceFolder = Path.Combine(srcFolder, projectFolder, "bin", configuration, framework);
            }

            return found;
        }

        static readonly Dictionary<string, string> projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"InMemory", "ServiceControl.Audit.Persistence.InMemory" },
            {"RavenDB5", "ServiceControl.Audit.Persistence.RavenDb5" }
        };
    }
}
