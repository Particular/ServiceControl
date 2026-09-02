namespace ServiceControl.Recoverability.Retrying.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

/// <summary>
/// One timed stretch of the retry pipeline. A stretch that finished counts as its outcome even if
/// shutdown has since been requested; one that shutdown cut short is recorded as cancelled rather
/// than as a failure.
/// </summary>
public sealed class RetryDurationScope : IDisposable
{
    internal RetryDurationScope(Histogram<double> histogram, TimeProvider timeProvider, TagList tags, CancellationToken cancellationToken)
    {
        this.histogram = histogram;
        this.timeProvider = timeProvider;
        this.tags = tags;
        this.cancellationToken = cancellationToken;
        start = timeProvider.GetTimestamp();
    }

    public void Complete() => Finish(ScopeOutcome.Success);

    public void Empty() => Finish(ScopeOutcome.Empty);

    public void Dispose()
    {
        var result = outcome ?? (cancellationToken.IsCancellationRequested ? ScopeOutcome.Cancelled : ScopeOutcome.Failed);
        tags.Add("result", ResultTag(result));

        histogram.Record(timeProvider.GetElapsedTime(start, end == 0 ? timeProvider.GetTimestamp() : end).TotalSeconds, tags);
    }

    // The work is over once it finishes, so the clock stops here rather than wherever the scope
    // happens to be disposed.
    void Finish(ScopeOutcome result)
    {
        end = timeProvider.GetTimestamp();
        outcome = result;
    }

    static string ResultTag(ScopeOutcome outcome) => outcome switch
    {
        ScopeOutcome.Success => "success",
        ScopeOutcome.Empty => "empty",
        ScopeOutcome.Cancelled => "cancelled",
        ScopeOutcome.Failed => "failed",
        _ => "failed"
    };

    ScopeOutcome? outcome;
    long end;
    TagList tags;

    readonly Histogram<double> histogram;
    readonly TimeProvider timeProvider;
    readonly CancellationToken cancellationToken;
    readonly long start;
}
