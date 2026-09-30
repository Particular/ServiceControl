namespace Particular.LicensingComponent;

using System.Globalization;
using Contracts;

/// <summary>
/// Folds the environment data of every responding audit instance into one value per key: values
/// that agree pass through, capacity numbers take the maximum, and anything else that differs
/// reports Mixed rather than guessing which instance is representative.
/// </summary>
static class AuditEnvironmentDataAggregator
{
    static readonly string[] MaximumKeys = ["Host.ProcessorCount", "Host.AvailableMemoryGB"];

    public static IEnumerable<KeyValuePair<string, string>> Aggregate(List<Dictionary<string, string>> instances)
    {
        if (instances is null)
        {
            yield break;
        }

        var keys = instances.SelectMany(instance => instance.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            var values = instances.Where(instance => instance.ContainsKey(key)).Select(instance => instance[key]).ToArray();

            yield return new(key, Aggregate(key, values));
        }
    }

    static string Aggregate(string key, string[] values)
    {
        if (values.Distinct(StringComparer.Ordinal).Count() == 1)
        {
            return values[0];
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
}
