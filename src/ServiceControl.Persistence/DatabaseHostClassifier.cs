namespace ServiceControl.Persistence;

using System;
using System.Linq;

/// <summary>
/// Classifies a database host name into a managed-service category. Only the category is ever
/// reported; the host name itself never leaves the process.
/// </summary>
/// <remarks>
/// This is the fallback for servers that cannot identify their own hosting. Where the engine can be
/// asked directly, as SQL Server can through EngineEdition, that answer wins over this one.
/// </remarks>
public static class DatabaseHostClassifier
{
    public const string Unknown = "Unknown";
    public const string SelfHosted = "SelfHosted";

    /// <summary>
    /// A host name is only ever evidence of a managed service or of a server on this machine.
    /// Anything else, a private DNS name above all, is unknown rather than self-hosted: a customer
    /// who fronts a managed database with their own DNS must not be counted as self-hosting it.
    /// </summary>
    public static string Classify(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return Unknown;
        }

        var normalized = host.Trim().ToLowerInvariant();

        foreach (var (suffix, hosting) in ManagedHostSuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal) || normalized.Contains($"{suffix},", StringComparison.Ordinal))
            {
                return hosting;
            }
        }

        // Cloud SQL is reached either through a host name or through a unix socket directory named
        // after the instance, so neither end of the value is a reliable place to look.
        if (normalized.Contains("cloudsql", StringComparison.Ordinal))
        {
            return "GoogleCloudSql";
        }

        return IsThisMachine(normalized) ? SelfHosted : Unknown;
    }

    static bool IsThisMachine(string normalized) =>
        LocalHosts.Contains(normalized) ||
        normalized.StartsWith("(localdb)", StringComparison.Ordinal) ||
        normalized.StartsWith("np:", StringComparison.Ordinal) ||
        normalized.StartsWith("lpc:", StringComparison.Ordinal) ||
        normalized.StartsWith('/'); // a unix socket directory, which only a server on this machine listens on

    static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1", "[::1]", ".", "(local)"];

    // Ordered longest-suffix-first so that the more specific Azure services win over the shared
    // .database.azure.com suffix.
    static readonly (string Suffix, string Hosting)[] ManagedHostSuffixes =
    [
        (".postgres.database.azure.com", "AzurePostgres"),
        (".mysql.database.azure.com", "AzureMySql"),
        (".database.windows.net", "AzureSql"),
        (".database.azure.com", "AzureSql"),
        (".rds.amazonaws.com", "AwsRds"),
        (".ravendb.cloud", "RavenCloud"),
        (".development.run", "RavenCloud"),
        (".gcp.cloud", "GoogleCloudSql")
    ];
}
