namespace Particular.LicensingComponent;

using System.Globalization;
using Contracts;

/// <summary>
/// Folds the environment data of every responding audit instance into one value per key: values
/// that agree pass through, capacity numbers take the maximum, sizes and counts sum with instances
/// on one database counted once, the sharing class takes the most shared, and anything else that
/// differs reports Mixed rather than guessing which instance is representative. Keys starting with
/// an underscore carry collection bookkeeping and are never emitted.
/// </summary>
static class AuditEnvironmentDataAggregator
{
    static readonly string[] MaximumKeys = ["Host.ProcessorCount", "Host.AvailableMemoryGB"];
    static readonly string[] SharingPrecedence = ["SameSchema", "SameDatabase", "SameServer", "SeparateServer", "NotApplicable", "Unknown"];

    public static IEnumerable<KeyValuePair<string, string>> Aggregate(List<Dictionary<string, string>> instances)
    {
        if (instances is null)
        {
            yield break;
        }

        var keys = instances
            .SelectMany(instance => instance.Keys)
            .Where(key => !key.StartsWith('_'))
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

        if (key == "Storage.MessageCount")
        {
            var sum = SumOncePerDatabase(contributions, value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null);

            if (sum is not null)
            {
                return ((long)sum.Value).ToString(CultureInfo.InvariantCulture);
            }
        }

        var values = contributions.Select(contribution => contribution.Value).ToArray();

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

        if (MaximumKeys.Contains(key, StringComparer.Ordinal))
        {
            var parsed = values.Select(value => (Parsed: long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number), Number: number)).ToArray();

            if (parsed.All(value => value.Parsed))
            {
                return parsed.Max(value => value.Number).ToString(CultureInfo.InvariantCulture);
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
