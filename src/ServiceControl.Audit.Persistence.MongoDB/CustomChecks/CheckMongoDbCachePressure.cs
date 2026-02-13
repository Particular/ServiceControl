namespace ServiceControl.Audit.Persistence.MongoDB.CustomChecks
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using global::MongoDB.Bson;

    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;

    class CheckMongoDbCachePressure(
        IMongoClientProvider clientProvider,
        MinimumRequiredStorageState stateHolder,
        ILogger<CheckMongoDbCachePressure> logger)
        : CustomCheck("MongoDB Storage Pressure", "ServiceControl.Audit Health", TimeSpan.FromSeconds(5))
    {
        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            try
            {
                if (clientProvider.ProductCapabilities.SupportsWiredTigerCacheMetrics)
                {
                    return await CheckWiredTigerCachePressure(cancellationToken).ConfigureAwait(false);
                }

                return await CheckWriteLatency(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check MongoDB storage pressure");

                // On failure, allow ingestion to continue — don't block on monitoring errors
                stateHolder.CanIngestMore = true;
                return CheckResult.Failed($"Unable to check MongoDB storage pressure: {ex.Message}");
            }
        }

        async Task<CheckResult> CheckWiredTigerCachePressure(CancellationToken cancellationToken)
        {
            var serverStatus = await clientProvider.Database
                .RunCommandAsync<BsonDocument>(new BsonDocument("serverStatus", 1), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var cache = serverStatus["wiredTiger"].AsBsonDocument["cache"].AsBsonDocument;

            var dirtyBytes = cache["tracked dirty bytes in the cache"].ToDouble();
            var totalBytes = cache["bytes currently in the cache"].ToDouble();
            var maxBytes = cache["maximum bytes configured"].ToDouble();

            var dirtyPercentage = maxBytes > 0 ? dirtyBytes / maxBytes * 100 : 0;
            var usedPercentage = maxBytes > 0 ? totalBytes / maxBytes * 100 : 0;

            logger.LogDebug(
                "MongoDB WiredTiger cache - Dirty: {DirtyPercentage:F1}%, Used: {UsedPercentage:F1}%, Dirty bytes: {DirtyBytes:N0}, Total bytes: {TotalBytes:N0}, Max bytes: {MaxBytes:N0}",
                dirtyPercentage, usedPercentage, dirtyBytes, totalBytes, maxBytes);

            if (dirtyPercentage >= DirtyThresholdPercentage)
            {
                logger.LogWarning(
                    "Audit message ingestion paused. MongoDB WiredTiger dirty cache at {DirtyPercentage:F1}% (threshold: {Threshold}%). This indicates write pressure is exceeding the storage engine's ability to flush to disk",
                    dirtyPercentage, DirtyThresholdPercentage);

                stateHolder.CanIngestMore = false;
                return CheckResult.Failed(
                    $"MongoDB WiredTiger dirty cache at {dirtyPercentage:F1}% (threshold: {DirtyThresholdPercentage}%). Ingestion paused to allow the storage engine to recover.");
            }

            stateHolder.CanIngestMore = true;
            return CheckResult.Pass;
        }

        async Task<CheckResult> CheckWriteLatency(CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            _ = await clientProvider.Database
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            sw.Stop();

            var latencyMs = sw.Elapsed.TotalMilliseconds;
            RecordLatency(latencyMs);

            var sampleCount = Math.Min(latencyIndex, LatencyWindowSize);
            var avgLatency = GetAverageLatency();

            logger.LogDebug(
                "MongoDB ping latency: {LatencyMs:F0}ms, Rolling average: {AvgLatency:F0}ms (samples: {SampleCount}/{WindowSize})",
                latencyMs, avgLatency, sampleCount, LatencyWindowSize);

            if (sampleCount >= MinSamplesBeforeThrottling && avgLatency >= LatencyThresholdMs)
            {
                logger.LogWarning(
                    "Audit message ingestion paused. MongoDB average response latency at {AvgLatency:F0}ms (threshold: {Threshold}ms). This indicates the database is under pressure",
                    avgLatency, LatencyThresholdMs);

                stateHolder.CanIngestMore = false;
                return CheckResult.Failed(
                    $"MongoDB average response latency at {avgLatency:F0}ms (threshold: {LatencyThresholdMs}ms). Ingestion paused to allow the database to recover.");
            }

            stateHolder.CanIngestMore = true;
            return CheckResult.Pass;
        }

        void RecordLatency(double latencyMs)
        {
            latencyWindow[latencyIndex % LatencyWindowSize] = latencyMs;
            latencyIndex++;
        }

        double GetAverageLatency()
        {
            var count = Math.Min(latencyIndex, LatencyWindowSize);
            if (count == 0)
            {
                return 0;
            }

            double sum = 0;
            for (var i = 0; i < count; i++)
            {
                sum += latencyWindow[i];
            }

            return sum / count;
        }

        // WiredTiger thresholds
        const double DirtyThresholdPercentage = 15;

        // Latency thresholds
        const int LatencyWindowSize = 6; // 30 seconds of history at 5-second intervals
        const int MinSamplesBeforeThrottling = 3; // Need at least 15 seconds of data before throttling
        const double LatencyThresholdMs = 500;
        readonly double[] latencyWindow = new double[LatencyWindowSize];
        int latencyIndex;
    }
}
