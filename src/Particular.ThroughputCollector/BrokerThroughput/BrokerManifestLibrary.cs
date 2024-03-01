namespace Particular.ThroughputCollector;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Particular.ThroughputCollector.Contracts;

public static class BrokerManifestLibrary
{
    public static List<BrokerSettings> BrokerSettings { get; private set; } = [];
    //TODO add logger
    static BrokerManifestLibrary()
    {
        var assemblyDirectory = GetAssemblyDirectory();

        try
        {
            if (assemblyDirectory != null)
            {
                var manifest = JsonSerializer.Deserialize<List<BrokerSettings>>(File.ReadAllText(Path.Combine(assemblyDirectory, "throughput.broker.manifest")));
                if (manifest != null)
                {
                    BrokerSettings = manifest;
                }
                else
                {
                    //logger.Warn($"Failed to deserialize broker manifest {manifestFile}");
                }
            }
        }
        catch (Exception ex)
        {
            //logger.Warn($"Failed to load broker settings from {assemblyDirectory}", ex);
            Console.WriteLine(ex.ToString());
        }
    }

    static string? GetAssemblyDirectory()
    {
        var assemblyLocation = typeof(BrokerManifestLibrary).Assembly.Location;
        return Path.GetDirectoryName(assemblyLocation);
    }

    public static BrokerSettings? Find(Contracts.Broker broker)
    {
        return BrokerSettings.FirstOrDefault(w => w.Broker == broker);
    }
}


