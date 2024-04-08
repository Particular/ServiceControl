namespace Particular.ThroughputCollector.Persistence;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NServiceBus.Logging;

public class PersistenceManifest
{
    public required string Version { get; set; }

    public string? Location { get; set; }

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string Description { get; set; }

    public required string TypeName { get; set; }

    public bool IsSupported { get; set; } = true;

    public string[] Aliases { get; set; } = [];

    internal bool IsMatch(string persistenceType) =>
        string.Equals(TypeName, persistenceType, StringComparison.Ordinal) // Type names are case-sensitive
        || string.Equals(Name, persistenceType, StringComparison.OrdinalIgnoreCase)
        || Aliases.Contains(persistenceType, StringComparer.Ordinal);
}

public static class PersistenceManifestLibrary
{
    public static List<PersistenceManifest> PersistenceManifests { get; } = [];

    static PersistenceManifestLibrary()
    {
        var assemblyDirectory = GetAssemblyDirectory();

        try
        {
            if (assemblyDirectory != null)
            {
                foreach (var manifestFile in Directory.EnumerateFiles(assemblyDirectory, "throughput.persistence.manifest", SearchOption.AllDirectories))
                {
                    AddManifest(manifestFile);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to load persistence manifests from {assemblyDirectory}", ex);
        }

        try
        {
            DevelopmentPersistenceLocations.ManifestFiles.ForEach(AddManifest);
        }
        catch (Exception ex)
        {
            logger.Warn($"Failed to load persistence manifests from development locations", ex);
        }

        PersistenceManifests.ForEach(m => logger.Info($"Found persistence manifest for {m.DisplayName}"));
    }

    static void AddManifest(string manifestFile)
    {
        var manifest = JsonSerializer.Deserialize<PersistenceManifest>(File.ReadAllText(manifestFile));
        if (manifest == null)
        {
            logger.Warn($"Failed to deserialize persistence manifest {manifestFile}");
            return;
        }

        var manifestPath = Path.GetDirectoryName(manifestFile);
        if (manifestPath == null)
        {
            logger.Warn($"Failed to get path of persistence manifest {manifestFile}");
            return;
        }

        manifest.Location = manifestPath;

        PersistenceManifests.Add(manifest);
    }

    static string? GetAssemblyDirectory()
    {
        var assemblyLocation = typeof(PersistenceManifestLibrary).Assembly.Location;
        return Path.GetDirectoryName(assemblyLocation);
    }

    public static PersistenceManifest? Find(string persistenceType)
    {
        if (persistenceType == null)
        {
            throw new Exception("No persistenceType has been configured. Either provide a Type or Name in the PersistenceType setting.");
        }

        return PersistenceManifests.FirstOrDefault(w => w.IsMatch(persistenceType));
    }

    static readonly ILog logger = LogManager.GetLogger(typeof(PersistenceManifestLibrary));
}


