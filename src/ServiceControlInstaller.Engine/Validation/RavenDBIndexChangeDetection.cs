namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Instances;

    public static class RavenDBIndexChangeDetection
    {
        // Returns null when the check is not possible: the instance does not use the embedded RavenDB, or its database
        // does not answer. The database answers only while the instance runs, so run this check before the instance stops.
        public static async Task<RavenDBIndexChanges> Detect(ServiceControlBaseService instance, CancellationToken cancellationToken = default)
        {
            if (instance is not IPersistenceConfig { PersistenceManifest.Name: StorageEngineNames.RavenDB } || instance.DatabaseMaintenancePort is not { } port)
            {
                return null;
            }

            var target = LoadBundledDefinitions(instance is ServiceControlAuditInstance ? "Particular.ServiceControl.Audit" : "Particular.ServiceControl");
            if (target == null)
            {
                return null;
            }

            try
            {
                using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}"), Timeout = TimeSpan.FromSeconds(15) };

                return await RavenDBIndexChanges.Detect(instance.Name, target, http.GetStringAsync, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                return null;
            }
        }

        // The index definitions of the version that this installer deploys.
        static List<RavenDBIndexDefinition> LoadBundledDefinitions(string instanceFolder)
        {
            var assembly = typeof(RavenDBIndexChangeDetection).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.Replace('/', '\\').Equals($@"{instanceFolder}\Persisters\RavenDB\{RavenDBIndexDefinitions.ManifestFileName}", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                return null;
            }

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName)!);
            return RavenDBIndexDefinitions.Parse(reader.ReadToEnd());
        }
    }
}
