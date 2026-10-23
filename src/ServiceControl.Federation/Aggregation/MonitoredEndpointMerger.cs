namespace ServiceControl.Federation.Aggregation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// Merges Monitoring responses. Endpoints are keyed by name only, so an endpoint reported by more than
/// one member is combined: counts and rates add up, timings are averaged weighted by throughput.
/// Every member uses the same history period, and each series has the same number of buckets aligned
/// to wall-clock UTC, so points combine index by index.
/// </summary>
public static class MonitoredEndpointMerger
{
    static readonly HashSet<string> SummedMetrics = new(StringComparer.OrdinalIgnoreCase) { "throughput", "retries", "queueLength" };

    /// <param name="memberEndpoints">One <c>monitored-endpoints</c> list per member, lead member first.</param>
    public static JsonArray MergeList(IReadOnlyList<IReadOnlyList<JsonNode>> memberEndpoints)
    {
        var byName = new Dictionary<string, List<JsonNode>>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var endpoints in memberEndpoints)
        {
            foreach (var endpoint in endpoints)
            {
                var name = JsonValues.Text(endpoint, "name") ?? "";
                if (!byName.TryGetValue(name, out var instances))
                {
                    instances = [];
                    byName[name] = instances;
                    order.Add(name);
                }

                instances.Add(endpoint);
            }
        }

        return [.. order.Select(name => MergeEndpoint(byName[name]))];
    }

    public static JsonObject MergeEndpoint(IReadOnlyList<JsonNode> instances)
    {
        var result = instances[0].DeepClone().AsObject();
        if (instances.Count == 1)
        {
            return result;
        }

        result["isStale"] = instances.All(i => JsonValues.Bool(i, "isStale") ?? false);
        result["connectedCount"] = instances.Sum(i => (long)(JsonValues.Number(i, "connectedCount") ?? 0));
        result["disconnectedCount"] = instances.Sum(i => (long)(JsonValues.Number(i, "disconnectedCount") ?? 0));
        result["endpointInstanceIds"] = new JsonArray([.. instances
            .SelectMany(i => JsonValues.At(i, "endpointInstanceIds") as JsonArray ?? [])
            .Select(id => id?.DeepClone())]);
        result["metrics"] = MergeMetrics([.. instances.Select(i => JsonValues.At(i, "metrics") as JsonObject)]);

        return result;
    }

    /// <summary>
    /// Merges <c>monitored-endpoints/{name}</c> details. Members that do not know the endpoint answer
    /// with an empty document, so only members reporting instances take part.
    /// </summary>
    public static JsonObject MergeDetails(IReadOnlyList<JsonNode> memberDetails)
    {
        var reporting = memberDetails
            .Where(d => (JsonValues.At(d, "instances") as JsonArray)?.Count > 0)
            .ToList();

        if (reporting.Count == 0)
        {
            return memberDetails.Count > 0 ? memberDetails[0].DeepClone().AsObject() : [];
        }

        var result = reporting[0].DeepClone().AsObject();
        if (reporting.Count == 1)
        {
            return result;
        }

        result["instances"] = new JsonArray([.. reporting
            .SelectMany(d => JsonValues.At(d, "instances") as JsonArray ?? [])
            .Select(i => i?.DeepClone())]);

        var messageTypes = new JsonArray();
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in reporting.SelectMany(d => JsonValues.At(d, "messageTypes") as JsonArray ?? []))
        {
            if (type != null && seenTypes.Add(JsonValues.Text(type, "id") ?? type.ToJsonString()))
            {
                messageTypes.Add(type.DeepClone());
            }
        }

        result["messageTypes"] = messageTypes;

        if (result["digest"] is JsonObject digest)
        {
            digest["metrics"] = MergeMetrics([.. reporting.Select(d => JsonValues.At(d, "digest.metrics") as JsonObject)]);
        }

        if (result["metricDetails"] is JsonObject metricDetails)
        {
            metricDetails["metrics"] = MergeMetrics([.. reporting.Select(d => JsonValues.At(d, "metricDetails.metrics") as JsonObject)]);
        }

        return result;
    }

    /// <summary>
    /// Merges metric maps (<c>{ name: { average, latest, points[], timeAxisValues[] } }</c>), one per member.
    /// </summary>
    public static JsonObject MergeMetrics(IReadOnlyList<JsonObject?> memberMetrics)
    {
        var present = memberMetrics.Where(m => m != null).Select(m => m!).ToList();
        if (present.Count == 0)
        {
            return [];
        }

        if (present.Count == 1)
        {
            return present[0].DeepClone().AsObject();
        }

        var result = new JsonObject();
        var names = present.SelectMany(m => m.Select(p => p.Key)).Distinct(StringComparer.Ordinal).ToList();
        var throughput = present.Select(m => m.TryGetPropertyValue("throughput", out var t) ? t : null).ToList();

        foreach (var name in names)
        {
            var values = present.Select(m => m.TryGetPropertyValue(name, out var v) ? v : null).ToList();
            var template = values.First(v => v != null)!.DeepClone().AsObject();
            var summed = SummedMetrics.Contains(name);

            foreach (var scalar in new[] { "average", "latest" })
            {
                if (template.ContainsKey(scalar))
                {
                    template[scalar] = CombineScalar(values, throughput, scalar, summed);
                }
            }

            if (template.ContainsKey("points"))
            {
                template["points"] = CombinePoints(values, throughput, summed);
            }

            result[name] = template;
        }

        return result;
    }

    static double? CombineScalar(IReadOnlyList<JsonNode?> values, IReadOnlyList<JsonNode?> throughput, string field, bool summed)
    {
        var samples = values
            .Select((v, i) => (Value: JsonValues.Number(v, field), Weight: JsonValues.Number(throughput[i], field) ?? 0))
            .Where(s => s.Value != null)
            .ToList();

        if (samples.Count == 0)
        {
            return null;
        }

        if (summed)
        {
            return samples.Sum(s => s.Value!.Value);
        }

        var totalWeight = samples.Sum(s => s.Weight);
        return totalWeight > 0
            ? samples.Sum(s => s.Value!.Value * s.Weight) / totalWeight
            : samples.Average(s => s.Value!.Value);
    }

    static JsonArray CombinePoints(IReadOnlyList<JsonNode?> values, IReadOnlyList<JsonNode?> throughput, bool summed)
    {
        var series = values.Select(v => JsonValues.At(v, "points") as JsonArray).ToList();
        var weights = throughput.Select(t => JsonValues.At(t, "points") as JsonArray).ToList();
        var length = series.Max(s => s?.Count ?? 0);
        var result = new JsonArray();

        for (var index = 0; index < length; index++)
        {
            var total = 0.0;
            var weightedTotal = 0.0;
            var totalWeight = 0.0;
            var samples = 0;

            for (var member = 0; member < series.Count; member++)
            {
                var point = series[member] is { } s && index < s.Count ? JsonValues.AsNumber(s[index]) : null;
                if (point == null)
                {
                    continue;
                }

                var weight = weights[member] is { } w && index < w.Count ? JsonValues.AsNumber(w[index]) ?? 0 : 0;
                total += point.Value;
                weightedTotal += point.Value * weight;
                totalWeight += weight;
                samples++;
            }

            if (samples == 0)
            {
                result.Add(0);
            }
            else if (summed)
            {
                result.Add(total);
            }
            else
            {
                result.Add(totalWeight > 0 ? weightedTotal / totalWeight : total / samples);
            }
        }

        return result;
    }
}
