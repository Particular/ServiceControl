namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;
using NServiceBus.Logging;

class CheckDirtyMemory(MemoryInformationRetriever memoryInformationRetriever) : CustomCheck("ServiceControl.Audit database", "Dirty memory trends", TimeSpan.FromMinutes(5))
{
    readonly List<int> lastDirtyMemoryReads = [];
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var (isHighDirty, dirtyMemoryKb) = await memoryInformationRetriever.GetMemoryInformation(cancellationToken);

        if (isHighDirty)
        {
            var message = $"There is a high level of dirty memory ({dirtyMemoryKb}kb). Check the ServiceControl " +
                          "troubleshooting guide for guidance on how to mitigate the issue.";
            Log.Warn(message);
            return CheckResult.Failed(message);
        }

        lastDirtyMemoryReads.Add(dirtyMemoryKb);
        if (lastDirtyMemoryReads.Count > 20)
        {
            //cap the list at 20 which means we're keeping about 1 hour and 40 minutes of data
            lastDirtyMemoryReads.RemoveAt(0);
        }

        switch (lastDirtyMemoryReads.Count)
        {
            case < 3:
                Log.Debug("Not enough dirty memory data in the series to calculate a trend.");
                break;
            // TODO do we need a threshold below which the check never fails?
            // Three means we'll be observing for 15 minutes before calculating the trend
            case >= 3 when AnalyzeTrendUsingRegression(lastDirtyMemoryReads) == TrendDirection.Increasing:
                {
                    var message = $"Dirty memory is increasing. Last available value is {dirtyMemoryKb}kb. " +
                                  $"Check the ServiceControl troubleshooting guide for guidance on how to mitigate the issue.";
                    Log.Warn(message);
                    return CheckResult.Failed(message);
                }

            default:
                // NOP
                break;
        }

        return CheckResult.Pass;
    }

    static TrendDirection AnalyzeTrendUsingRegression(List<int> values)
    {
        if (values is not { Count: > 1 })
        {
            throw new ArgumentException("Need at least two values to determine a trend");
        }

        // Calculate slope using linear regression
        double numberOfPoints = values.Count;
        double sumOfIndices = 0;
        double sumOfValues = 0;
        double sumOfIndicesMultipliedByValues = 0;
        double sumOfIndicesSquared = 0;

        for (int i = 0; i < values.Count; i++)
        {
            double index = i;
            double value = values[i];

            sumOfIndices += index;
            sumOfValues += value;
            sumOfIndicesMultipliedByValues += index * value;
            sumOfIndicesSquared += index * index;
        }

        // Slope formula: (n*Σxy - Σx*Σy) / (n*Σx² - (Σx)²)
        double slopeNumerator = (numberOfPoints * sumOfIndicesMultipliedByValues) - (sumOfIndices * sumOfValues);
        double slopeDenominator = (numberOfPoints * sumOfIndicesSquared) - (sumOfIndices * sumOfIndices);
        double slope = slopeNumerator / slopeDenominator;

        // Determine trend based on slope
        const double slopeThreshold = 0.001; // Small threshold to handle floating-point precision
        if (Math.Abs(slope) < slopeThreshold)
        {
            return TrendDirection.Flat;
        }

        return slope > 0 ? TrendDirection.Increasing : TrendDirection.Decreasing;
    }

    enum TrendDirection
    {
        Increasing,
        Decreasing,
        Flat
    }

    static readonly ILog Log = LogManager.GetLogger<CheckDirtyMemory>();
}