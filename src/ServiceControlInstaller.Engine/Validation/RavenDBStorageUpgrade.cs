namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.ServiceProcess;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Instances;

    public class RavenDBStorageUpgrade
    {
        public required string InstanceName { get; init; }

        public required bool IsAuditInstance { get; init; }

        public required long DatabaseSizeBytes { get; init; }

        public long? StoredMessages { get; init; }

        public TimeSpan? EstimatedDuration => StoredMessages is { } count ? TimeSpan.FromSeconds(count / BaselineMessagesPerSecond) : null;

        public static async Task<RavenDBStorageUpgrade> Detect(ServiceControlBaseService instance, CancellationToken cancellationToken = default)
        {
            if (!ConvertsDatabase(instance))
            {
                return null;
            }

            var isAuditInstance = instance is ServiceControlAuditInstance;

            return new RavenDBStorageUpgrade
            {
                InstanceName = instance.Name,
                IsAuditInstance = isAuditInstance,
                DatabaseSizeBytes = DatabaseSize(instance.DBPath),
                StoredMessages = isAuditInstance && IsRunning(instance)
                    ? await TryGetStoredMessageCount(MessagesUri(instance), cancellationToken: cancellationToken).ConfigureAwait(false)
                    : null
            };
        }

        // Every instance ships the RavenDB server, so the server files alone do not show that the instance stores its data in it.
        public static bool ConvertsDatabase(ServiceControlBaseService instance) =>
            instance is IPersistenceConfig { PersistenceManifest.Name: StorageEngineNames.RavenDB }
            && !UsesExternalRavenDB(instance)
            && RavenDBStorageSchema.RequiresDataUpgrade(instance.InstallPath);

        static bool UsesExternalRavenDB(ServiceControlBaseService instance) =>
            UsesExternalRavenDB(
                instance is ServiceControlAuditInstance ? "ServiceControl.Audit" : "ServiceControl",
                key => instance.AppConfig?.Config.AppSettings.Settings[key]?.Value,
                name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine));

        // Mirrors how the instance reads RavenDB/ConnectionString: from app.config, or from a namespaced or un-namespaced environment variable.
        internal static bool UsesExternalRavenDB(string settingsNamespace, Func<string, string> readAppSetting, Func<string, string> readEnvironmentVariable)
        {
            if (!string.IsNullOrWhiteSpace(readAppSetting($"{settingsNamespace}/{ConnectionStringSetting}")))
            {
                return true;
            }

            var namespacedName = $"{settingsNamespace}_{ConnectionStringSetting}".Replace('.', '_').Replace('/', '_').ToUpperInvariant();
            var name = ConnectionStringSetting.Replace('/', '_').ToUpperInvariant();
            return !string.IsNullOrWhiteSpace(readEnvironmentVariable(namespacedName)) || !string.IsNullOrWhiteSpace(readEnvironmentVariable(name));
        }

        static bool IsRunning(BaseService instance)
        {
            try
            {
                instance.Service.Refresh();
                return instance.Service.Status == ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public string Describe()
        {
            var text = new StringBuilder();
            text.AppendLine($"Upgrading {InstanceName} converts its RavenDB database to a new storage format. The conversion runs once, while the instance is set up, and the instance is unavailable until it finishes.");
            text.AppendLine();
            text.AppendLine($"Database size: {FormatSize(DatabaseSizeBytes)}");

            if (StoredMessages is { } count && EstimatedDuration is { } duration)
            {
                text.AppendLine($"Stored messages: {count.ToString("N0", CultureInfo.InvariantCulture)}");
                text.AppendLine();
                text.AppendLine($"The duration depends on the number of stored messages and on disk latency. On hardware that meets the recommended baseline, expect about {FormatDuration(duration)} or longer.");
            }
            else
            {
                text.AppendLine();
                text.AppendLine("The duration depends on the number of stored messages and on disk latency. For a large database, it can take hours or days.");
            }

            text.AppendLine();
            text.AppendLine("Do not stop the instance setup while the conversion runs. Stopping it starts the conversion over.");

            if (IsAuditInstance)
            {
                text.AppendLine();
                text.AppendLine($"For a large audit database, consider adding a new audit instance instead and removing this one when its audit retention period has passed: {ReplaceAuditInstanceGuideUrl}");
            }

            return text.ToString();
        }

        public static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.FromMinutes(1))
            {
                return "less than a minute";
            }

            if (duration < TimeSpan.FromHours(1))
            {
                return Pluralize((int)duration.TotalMinutes, "minute");
            }

            if (duration < TimeSpan.FromDays(1))
            {
                var hours = Pluralize((int)duration.TotalHours, "hour");
                return duration.Minutes == 0 ? hours : $"{hours} {Pluralize(duration.Minutes, "minute")}";
            }

            var days = Pluralize((int)duration.TotalDays, "day");
            return duration.Hours == 0 ? days : $"{days} {Pluralize(duration.Hours, "hour")}";
        }

        internal static string FormatSize(long bytes) =>
            bytes >= GiB
                ? $"{(bytes / (double)GiB).ToString("N1", CultureInfo.InvariantCulture)} GB"
                : $"{(bytes / (double)MiB).ToString("N0", CultureInfo.InvariantCulture)} MB";

        internal static long DatabaseSize(string dbPath)
        {
            var databases = new DirectoryInfo(Path.Combine(dbPath, "Databases"));
            if (!databases.Exists)
            {
                return 0;
            }

            return databases.EnumerateDirectories()
                .Select(database => new FileInfo(Path.Combine(database.FullName, "Raven.voron")))
                .Where(dataFile => dataFile.Exists)
                .Sum(dataFile => dataFile.Length);
        }

        static Uri MessagesUri(ServiceControlBaseService instance) =>
            MessagesUri(instance.UrlScheme, instance.HostName, instance.Port, instance.VirtualDirectory);

        internal static Uri MessagesUri(string scheme, string hostName, int port, string virtualDirectory)
        {
            var host = hostName is "*" or "+" ? "localhost" : hostName;
            var path = string.IsNullOrWhiteSpace(virtualDirectory) ? "" : virtualDirectory.Trim('/') + "/";
            return new Uri($"{scheme}://{host}:{port}/{path}api/messages?include_system_messages=true&per_page=1");
        }

        // Best effort: the instance may be stopped, require authentication, or use a certificate this machine does not trust.
        internal static async Task<long?> TryGetStoredMessageCount(Uri messagesUri, HttpMessageHandler handler = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = handler == null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
                client.Timeout = TimeSpan.FromSeconds(5);

                using var response = await client.GetAsync(messagesUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || !response.Headers.TryGetValues("Total-Count", out var values))
                {
                    return null;
                }

                return long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        static string Pluralize(int value, string unit) => value == 1 ? $"1 {unit}" : $"{value} {unit}s";

        // Preliminary: about 1,200 uncached reads per second on the baseline disk, divided by about 2 uncached reads per stored message.
        internal const double BaselineMessagesPerSecond = 600;

        const string ConnectionStringSetting = "RavenDB/ConnectionString";
        const string ReplaceAuditInstanceGuideUrl = "https://docs.particular.net/servicecontrol/migrations/replacing-audit-instances/";
        const long MiB = 1024 * 1024;
        const long GiB = 1024 * MiB;
    }
}
