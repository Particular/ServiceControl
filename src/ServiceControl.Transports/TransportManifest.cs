namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using NServiceBus.Logging;

    public class TransportManifest
    {
        public string Version { get; set; }

        public TransportManifestDefinition[] Definitions { get; set; }
    }

    public class TransportManifestDefinition
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string TypeName { get; set; }

        public string[] Aliases { get; set; }

        internal bool IsMatch(string transportType) =>
            string.Compare(TypeName, transportType, true) == 0
            || string.Compare(Name, transportType, true) == 0
            || AliasesContain(transportType);

        bool AliasesContain(string transportType)
        {
            if (Aliases == null)
            {
                return false;
            }

            return Aliases.Contains(transportType);
        }
    }

    public static class TransportManifestLibrary
    {
        public static List<TransportManifest> TransportManifests { get; set; }

        static bool initialized;

        static bool usingDevelopmentLocation;

        static void Initialize(string transportType)
        {
            if (TransportManifests == null)
            {
                TransportManifests = new List<TransportManifest>();
            }

            if (!initialized)
            {
                initialized = true;
                var transportFolder = GetAssemblyDirectory();

                try
                {
                    TransportManifests.AddRange(
                        Directory.EnumerateFiles(transportFolder, "transport.manifest", SearchOption.AllDirectories)
                        .Select(manifest => JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifest)))
                        );

                    if (TransportManifests.Count == 0 && DevelopmentTransportLocations.TryGetTransportFolder(transportType, out transportFolder))
                    {
                        var manifest = Path.Combine(transportFolder, "transport.manifest");
                        TransportManifests.Add(JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifest)));

                        usingDevelopmentLocation = true;
                    }

                    TransportManifests.SelectMany(t => t.Definitions).ToList().ForEach(m => logger.Info($"Found transport manifest for {m.DisplayName}"));
                }
                catch (Exception ex)
                {
                    logger.Warn($"Failed to load transport manifests from {transportFolder}", ex);
                }
            }
        }

        static string GetAssemblyDirectory()
        {
            var assemblyLocation = typeof(TransportManifestLibrary).Assembly.Location;
            return Path.GetDirectoryName(assemblyLocation);
        }

        public static string Find(string transportType)
        {
            if (transportType == null)
            {
                throw new Exception("No transport has been configured. Either provide a Type or Name in the TransportType setting.");
            }

            Initialize(transportType);

            var transportManifestDefinition = TransportManifests
                .SelectMany(t => t.Definitions)
                .FirstOrDefault(w => w.IsMatch(transportType));

            if (transportManifestDefinition != null)
            {
                return transportManifestDefinition.TypeName;
            }

            return transportType;
        }

        public static string GetTransportFolder(string transportType)
        {
            if (transportType == null)
            {
                throw new Exception("No transport has been configured. Either provide a Type or Name in the TransportType setting.");
            }

            Initialize(transportType);

            var transportManifestDefinition = TransportManifests
                .SelectMany(t => t.Definitions)
                .FirstOrDefault(w => w.IsMatch(transportType));

            string transportFolder = null;

            if (transportManifestDefinition != null)
            {
                if (usingDevelopmentLocation)
                {
                    _ = DevelopmentTransportLocations.TryGetTransportFolder(transportManifestDefinition.Name, out transportFolder);
                }
                else
                {
                    var appDirectory = GetAssemblyDirectory();
                    var transportName = transportManifestDefinition.Name.Split('.').FirstOrDefault();
                    transportFolder = Path.Combine(appDirectory, "Transports", transportName);
                }
            }

            return transportFolder;
        }

        static readonly ILog logger = LogManager.GetLogger(typeof(TransportManifestLibrary));
    }
}


