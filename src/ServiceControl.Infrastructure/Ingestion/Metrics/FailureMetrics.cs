namespace ServiceControl.Infrastructure.Ingestion.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// One message the ingestion could not handle. Leaving the scope without saying otherwise records
/// it as having been given up on and stored as a failed import.
/// </summary>
public sealed class FailureMetrics(TagList messageTags, Counter<long> failures) : IDisposable
{
    public void Retry() => retry = true;

    public void Dispose()
    {
        var tags = messageTags;
        tags.Add("result", retry ? "retry" : "stored-poison");

        failures.Add(1, tags);
    }

    bool retry;
}