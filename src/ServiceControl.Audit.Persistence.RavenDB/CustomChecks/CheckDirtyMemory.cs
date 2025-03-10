namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;
using NServiceBus.Logging;

class CheckDirtyMemory(DatabaseConfiguration databaseConfiguration) : CustomCheck("ServiceControl.Audit database", "Dirty memory trends", TimeSpan.FromMinutes(5))
{
    readonly List<int> lastDirtyMemoryReads = [];
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var retriever = await GetMemoryRetriever();
        var memoryInfo = await retriever.GetMemoryInformation(cancellationToken);

        if (memoryInfo.IsHighDirty)
        {
            var message = $"There is a high level of dirty memory ({memoryInfo.DirtyMemory}kb). Check the ServiceControl " +
                          "troubleshooting guide for guidance on how to mitigate the issue.";
            Log.Warn(message);
            return CheckResult.Failed(message);
        }

        lastDirtyMemoryReads.Add(memoryInfo.DirtyMemory);
        if (lastDirtyMemoryReads.Count > 20)
        {
            //cap the list at 20 which means we're keeping about 1 hour and 40 minutes of data
            lastDirtyMemoryReads.RemoveAt(0);
        }

        if (lastDirtyMemoryReads.Count < 3)
        {
            Log.Debug("Not enough dirty memory data in the series to calculate a trend.");
        }

        // TODO do we need a threshold below which the check never fails?
        // Three means we'll be observing for 15 minutes before calculating the trend
        if (lastDirtyMemoryReads.Count >= 3 && AnalyzeTrendUsingRegression(lastDirtyMemoryReads) == TrendDirection.Increasing)
        {
            var message = $"Dirty memory is increasing. Last available value is {memoryInfo.DirtyMemory}kb. " +
                          $"Check the ServiceControl troubleshooting guide for guidance on how to mitigate the issue.";
            Log.Warn(message);
            return CheckResult.Failed(message);
        }

        return CheckResult.Pass;
    }

    MemoryInformationRetriever _retriever;
    async Task<MemoryInformationRetriever> GetMemoryRetriever() => _retriever ??= new MemoryInformationRetriever(databaseConfiguration.ServerConfiguration.ServerUrl);

    static TrendDirection AnalyzeTrendUsingRegression(List<int> values)
    {
        if (values == null || values.Count <= 1)
        {
            throw new ArgumentException("Need at least two values to determine a trend");
        }

        // Calculate slope using linear regression
        double n = values.Count;
        double sumX = 0;
        double sumY = 0;
        double sumXY = 0;
        double sumXX = 0;

        for (int i = 0; i < values.Count; i++)
        {
            double x = i;
            double y = values[i];

            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumXX += x * x;
        }

        double slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);

        // Determine trend based on slope
        if (Math.Abs(slope) < 0.001) // Small threshold to handle floating-point precision
        {
            return TrendDirection.Flat;
        }

        if (slope > 0)
        {
            return TrendDirection.Increasing;
        }

        return TrendDirection.Decreasing;
    }

    enum TrendDirection
    {
        Increasing,
        Decreasing,
        Flat
    }

    static readonly ILog Log = LogManager.GetLogger<CheckDirtyMemory>();
}