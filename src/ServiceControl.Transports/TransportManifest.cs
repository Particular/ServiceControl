namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    public class TransportManifest
    {
        public string Version { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public List<TransportManifestCustomization> Customizations { get; set; }
    }

    public class TransportManifestCustomization
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string TypeName { get; set; }
    }

    public static class TransportManifestLibrary
    {
        public static List<TransportManifest> TransportManifests { get; set; }

        static bool initialized;
        public static void Initialize()
        {
            if (!initialized)
            {
                initialized = true;
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                Directory.EnumerateFiles(assemblyLocation, "transport.manifest", SearchOption.AllDirectories).ToList().ForEach(manifest =>
                {
                    if (TransportManifests == null)
                    {
                        TransportManifests = new List<TransportManifest>();
                    }

                    TransportManifests.Add(System.Text.Json.JsonSerializer.Deserialize<TransportManifest>(File.ReadAllText(manifest)));
                });
            }
        }

        public static string Find(string transportType, string transportName)
        {
            if (transportType == null && transportName == null)
            {
                throw new Exception("No transport have been configured. Either provide a TransportType setting or a TransportName setting.");
            }

            if (transportType == null)
            {
                var multipleCustomizationsPerManifest = transportName.IndexOf('.') != -1;
                var transportNameMain = multipleCustomizationsPerManifest ? transportName.Split('.').First() : transportName;

                var transportManifest = TransportManifests.Where(w => w.Name == transportNameMain).FirstOrDefault();
                if (transportManifest != null)
                {
                    if (multipleCustomizationsPerManifest)
                    {
                        return transportManifest.Customizations.Where(w => w.Name == transportName.Split('.')[1]).FirstOrDefault().TypeName;
                    }

                    return transportManifest.TypeName;
                }
            }

            return transportType;
        }
    }
}


