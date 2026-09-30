namespace Particular.LicensingComponent;

using System.Globalization;
using Contracts;

static class ThroughputDataExtensions
{
    public static IEnumerable<EndpointDailyThroughput> FromSource(this List<ThroughputData> throughputs, ThroughputSource source) => throughputs
        .Where(td => td.ThroughputSource == source)
        .SelectMany(td => td)
        .Select(kvp => new EndpointDailyThroughput(kvp.Key, kvp.Value));

    public static long Sum(this List<ThroughputData> throughputs) => throughputs.SelectMany(t => t).Sum(kvp => kvp.Value);

    public static long MaxDailyThroughput(this Dictionary<DateOnly, long> dailyThroughput)
        => dailyThroughput switch
        {
            { Count: 0 } => 0,
            var x => x.Values.Max()
        };

    public static Dictionary<DateOnly, long> DailyThroughput(this List<ThroughputData> throughputs) =>
        throughputs.SelectMany(
            throughput => throughput.Select(
                daily => (
                    Source: throughput.ThroughputSource,
                    Date: daily.Key,
                    Throughput: daily.Value
                )
            )
        )
        // Older SQL Reports could return a negative value for daily throughput. These are not valid. See https://github.com/Particular/ServiceControl/pull/5404
        .Where(entry => entry.Throughput >= 0)
        .GroupBy(entry => entry.Date)
        .ToDictionary(
            entries => entries.Key,
            entries => entries.OrderBy(entry => entry.Source switch
                {
                    ThroughputSource.Endpoint => 0,
                    ThroughputSource.Audit or ThroughputSource.Monitoring => 1,
                    ThroughputSource.Broker => 2,
                    _ => int.MaxValue
                })
                .ThenByDescending(entry => entry.Throughput)
                .Select(entry => entry.Throughput)
                .First()
        );

    public static MonthlyThroughput[] MonthlyThroughput(this Dictionary<DateOnly, long> dailyThroughput) => [
        ..dailyThroughput
        .GroupBy(kvp => $"{kvp.Key:yyyy-MM}", kvp => kvp.Value)
        .Select(group => new MonthlyThroughput(group.Key, group.Sum()))
    ];


    public static long AverageMonthlyThroughput(this Dictionary<DateOnly, long> dailyThroughput)
        => dailyThroughput switch
        {
            { Count: 0 } => 0,
            var throughput => (long)Math.Truncate(throughput.Sum(x => x.Value) / (decimal)throughput.Count * 365 / 12)
        };

    public static bool HasDataFromSource(this IDictionary<string, IEnumerable<ThroughputData>> throughputPerQueue, ThroughputSource source) =>
        throughputPerQueue.Any(queueThroughput => queueThroughput.Value.Any(data => data.ThroughputSource == source && data.Count > 0));
}