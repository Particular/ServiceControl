namespace Particular.LicensingComponent;

using System.Globalization;
using Contracts;

/// <summary>
/// Folds the environment data of every responding audit instance into one value per key: values
/// that agree pass through, capacity and saturation numbers take the maximum, uptime takes the
/// minimum, sizes and counts sum with instances on one database counted once, ingestion rates sum
/// across all instances, the sharing class takes the most shared, and anything else that differs
/// reports Mixed rather than guessing which instance is representative. Only keys in
/// <see cref="ReportedKeys"/> are emitted, so a key an audit instance of another version serves, or
/// the bookkeeping keys the primary adds, never reach the report unreviewed.
/// </summary>
static class AuditEnvironmentDataAggregator
{
    static readonly HashSet<string> ReportedKeys = new(StringComparer.Ordinal)
    {
        "Host.Model",
        "Host.Orchestrator",
        "Host.OSPlatform",
        "Host.OSVersion",
        "Host.Architecture",
        "Host.RuntimeVersion",
        "Host.ProcessorCount",
        "Host.AvailableMemoryGB",
        "Storage.Type",
        "Storage.RavenServer",
        "Storage.Hosting",
        "Storage.ServerVersion",
        "Storage.HostingSource",
        "Storage.ServerEdition",
        "Storage.ServiceObjective",
        "Storage.SizeGB",
        "Storage.MessageCount",
        "Storage.FullTextSearch",
        "Health.FailedImports",
        "Health.UptimeHours",
        "Health.LagOver1MinPercent",
        "Health.LagOver10MinPercent",
        "Health.LagOver60MinPercent",
        "Ingestion.AvgDailyMessages",
        "Ingestion.BusyPercent",
        "SameMachine",
        "DatabaseSharing"
    };

    static readonly string[] MaximumKeys = ["Host.ProcessorCount", "Host.AvailableMemoryGB", "Ingestion.BusyPercent", "Health.LagOver1MinPercent", "Health.LagOver10MinPercent", "Health.LagOver60MinPercent"];
    static readonly string[] MinimumKeys = ["Health.UptimeHours"];
    static readonly string[] SharingPrecedence = ["SameSchema", "SameDatabase", "SameServer", "SeparateServer", "NotApplicable", "Unknown"];

    public static IEnumerable<KeyValuePair<string, string>> Aggregate(List<Dictionary<string, string>> instances)
    {
        if (instances is null)
        {
            yield break;
        }

        var keys = instances
            .SelectMany(instance => instance.Keys)
            .Where(ReportedKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            var contributions = instances
                .Where(instance => instance.ContainsKey(key))
                .Select((instance, index) => (Value: instance[key], DatabaseKey: instance.GetValueOrDefault(AuditEnvironmentMetadata.DatabaseKey) ?? $"_{index}"))
                .ToArray();

            yield return new(key, Aggregate(key, contributions));
        }
    }

    static string Aggregate(string key, (string Value, string DatabaseKey)[] contributions)
    {
        if (key == "Storage.SizeGB")
        {
            var sum = SumOncePerDatabase(contributions, value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) ? size : null);

            if (sum is not null)
            {
                return sum.Value.ToString("F1", CultureInfo.InvariantCulture);
            }
        }

        if (key is "Storage.MessageCount" or "Health.FailedImports")
        {
            var sum = SumOncePerDatabase(contributions, value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null);

            if (sum is not null)
            {
                return ((long)sum.Value).ToString(CultureInfo.InvariantCulture);
            }
        }

        var values = contributions.Select(contribution => contribution.Value).ToArray();

        if (key == "Ingestion.AvgDailyMessages")
        {
            var parsed = values.Select(value => (Parsed: long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number), Number: number)).ToArray();

            if (parsed.All(value => value.Parsed))
            {
                return parsed.Sum(value => value.Number).ToString(CultureInfo.InvariantCulture);
            }
        }

        if (values.Distinct(StringComparer.Ordinal).Count() == 1)
        {
            return values[0];
        }

        // The most-shared class across instances, because one shared database matters however many
        // separate ones sit beside it.
        if (key == "DatabaseSharing")
        {
            foreach (var sharing in SharingPrecedence)
            {
                if (values.Contains(sharing, StringComparer.Ordinal))
                {
                    return sharing;
                }
            }
        }

        if (MaximumKeys.Contains(key, StringComparer.Ordinal) || MinimumKeys.Contains(key, StringComparer.Ordinal))
        {
            var parsed = values.Select(value => (Parsed: long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number), Number: number)).ToArray();

            if (parsed.All(value => value.Parsed))
            {
                var numbers = parsed.Select(value => value.Number);

                return (MinimumKeys.Contains(key, StringComparer.Ordinal) ? numbers.Min() : numbers.Max()).ToString(CultureInfo.InvariantCulture);
            }
        }

        return "Mixed";
    }

    static double? SumOncePerDatabase((string Value, string DatabaseKey)[] contributions, Func<string, double?> parse)
    {
        var parsed = contributions
            .Select(contribution => (contribution.DatabaseKey, Number: parse(contribution.Value)))
            .Where(contribution => contribution.Number is not null)
            .ToArray();

        if (parsed.Length == 0)
        {
            return null;
        }

        return parsed
            .GroupBy(contribution => contribution.DatabaseKey, StringComparer.Ordinal)
            .Sum(group => group.Max(contribution => contribution.Number!.Value));
    }
}
