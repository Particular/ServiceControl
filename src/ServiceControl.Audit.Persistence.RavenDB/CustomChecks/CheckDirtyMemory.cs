namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;

class CheckDirtyMemory(IRavenDocumentStoreProvider documentStoreProvider) : CustomCheck("ServiceControl.Audit database", "Dirty memory trends", TimeSpan.FromMinutes(5))
{
    readonly List<int> lastDirtyMemoryReads = [];
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var retriever = await GetMemoryRetriever(cancellationToken);
        var memoryInfo = await retriever.GetMemoryInformation(cancellationToken);

        if (memoryInfo.IsHighDirty)
        {
            //log warning
            return CheckResult.Failed("There is a high level of dirty memory. Check the ServiceControl " +
                                      "troubleshooting guide for guidance on how to mitigate the issue.");
        }

        lastDirtyMemoryReads.Add(memoryInfo.DirtyMemory);
        if (lastDirtyMemoryReads.Count > 20)
        {
            //cap the list at 20
            lastDirtyMemoryReads.RemoveAt(lastDirtyMemoryReads.Count - 1);
        }

        if (lastDirtyMemoryReads.Count > 3 && AnalyzeTrendUsingRegression(lastDirtyMemoryReads) == TrendDirection.Increasing) // Three means we'll be observing for 15 minutes before calculating the trend
        {
            // log a warning and fail the check
        }

        return CheckResult.Pass;
    }

    MemoryInformationRetriever _retriever;
    async Task<MemoryInformationRetriever> GetMemoryRetriever(CancellationToken cancellationToken = default)
    {
        if (_retriever == null)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var serverUrl = documentStore.Urls[0]; //TODO is there a better way to get the RavenDB server URL?
            _retriever = new MemoryInformationRetriever(serverUrl);
        }
        return _retriever;
    }

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
        Flat,
        Mixed
    }
}