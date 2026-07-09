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

    // Daily throughput for an endpoint. When multiple sources have data for the same day,
    // the maximum value is used, since all sources measure the same messages.
    public static IEnumerable<EndpointDailyThroughput> DailyThroughput(this List<ThroughputData> throughputs) => throughputs
        .SelectMany(td => td)
        .GroupBy(kvp => kvp.Key)
        .Select(group => new EndpointDailyThroughput(group.Key, group.Max(kvp => kvp.Value)));

    public static MonthlyThroughput[] MonthlyThroughput(this List<ThroughputData> throughputs) => [.. throughputs
            .SelectMany(data => data)
            .GroupBy(kvp => $"{kvp.Key:yyyy-MM}")
            .Select(group => new MonthlyThroughput(group.Key, group.Sum(kvp => kvp.Value)))];

    public static long MaxMonthlyThroughput(this List<ThroughputData> throughputs)
    {
        var monthlySums = throughputs
            .SelectMany(data => data)
            .GroupBy(kvp => $"{kvp.Key.Year}-{kvp.Key.Month}")
            .Select(group => group.Sum(kvp => kvp.Value))
            .ToArray();

        if (monthlySums.Any())
        {
            return monthlySums.Max();
        }

        return 0;
    }

    public static bool HasDataFromSource(this IDictionary<string, IEnumerable<ThroughputData>> throughputPerQueue, ThroughputSource source) =>
        throughputPerQueue.Any(queueName => queueName.Value.Any(data => data.ThroughputSource == source && data.Count > 0));
}