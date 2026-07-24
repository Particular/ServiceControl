namespace Particular.LicensingComponent;

using Contracts;

static class ThroughputDataExtensions
{
    public static IEnumerable<EndpointDailyThroughput> FromSource(this List<ThroughputData> throughputs, ThroughputSource source) => throughputs
        .Where(td => td.ThroughputSource == source)
        .SelectMany(td => td)
        .Select(kvp => new EndpointDailyThroughput(kvp.Key, kvp.Value));

    public static long Sum(this List<ThroughputData> throughputs) => throughputs.SelectMany(t => t).Sum(kvp => kvp.Value);

    public static long MaxDailyThroughput(this List<ThroughputData> throughputs)
    {
        var items = throughputs.SelectMany(t => t).ToArray();

        if (items.Any())
        {
            return items.Max(kvp => kvp.Value);
        }

        return 0;
    }

    public static MonthlyThroughput[] MonthlyThroughput(this List<ThroughputData> throughputs) => [.. throughputs
            .SelectMany(data => data)
            // Older SQL Reports could return a negative value for daily throughput. These are not valid. See https://github.com/Particular/ServiceControl/pull/5404
            .Where(x => x.Value >= 0)
            .GroupBy(x => x.Key, x => x.Value)
            .ToLookup(x => x.Key, x => x.Max())
            .GroupBy(kvp => $"{kvp.Key:yyyy-MM}", x => x.Sum())
            .Select(group => new MonthlyThroughput(group.Key, group.Sum()))];

    public static long AverageMonthlyThroughput(this List<ThroughputData> throughputs)
    {
        if (!throughputs.Any(x => x.Any()))
        {
            return 0;
        }

        // keep this in sync with the internal licensing calculation
        var maxDailyThroughput = throughputs
            .SelectMany(x => x)
            .Where(x => x.Value >= 0)
            .GroupBy(x => x.Key, x => x.Value)
            .ToLookup(x => x.Key, x => x.Max())
            .ToDictionary(x => x.Key, x => x.Sum());

        return (long)Math.Truncate(maxDailyThroughput.Sum(x => x.Value) / (decimal)maxDailyThroughput.Count * 365 / 12);
    }

    public static bool HasDataFromSource(this IDictionary<string, IEnumerable<ThroughputData>> throughputPerQueue, ThroughputSource source) =>
        throughputPerQueue.Any(queueName => queueName.Value.Any(data => data.ThroughputSource == source && data.Count > 0));
}