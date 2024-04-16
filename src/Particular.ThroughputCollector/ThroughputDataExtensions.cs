namespace Particular.ThroughputCollector;

using System.Linq;
using Particular.ThroughputCollector.Contracts;

static class ThroughputDataExtensions
{
    public static IEnumerable<EndpointDailyThroughput> FromSource(this List<ThroughputData> throughputs, ThroughputSource source) => throughputs
        .SingleOrDefault(td => td.ThroughputSource == source)?
        .Select(kvp => new EndpointDailyThroughput(kvp.Key, kvp.Value)) ?? [];

    public static long Sum(this List<ThroughputData> throughputs) => throughputs.SelectMany(t => t).Sum(kvp => kvp.Value);

    public static long Max(this List<ThroughputData> throughputs) =>
        throughputs.Any()
            ? throughputs.SelectMany(t => t).Max(kvp => kvp.Value)
            : 0;

    public static bool HasDataFromSource(this IDictionary<string, IEnumerable<ThroughputData>> throughputPerQueue, ThroughputSource source) =>
        throughputPerQueue.Any(queueName => queueName.Value.Any(data => data.ThroughputSource == source && data.Count > 0));
}
