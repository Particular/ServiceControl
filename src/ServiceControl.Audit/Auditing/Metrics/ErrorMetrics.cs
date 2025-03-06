namespace ServiceControl.Audit.Auditing.Metrics;

using System;
using System.Diagnostics.Metrics;
using NServiceBus.Transport;

public record ErrorMetrics(ErrorContext Context, Counter<long> Failures) : IDisposable
{
    public void Dispose()
    {
        var tags = IngestionMetrics.GetMessageTags(Context.Message.Headers);

        tags.Add("result", retry ? "retry" : "stored-poison");

        Failures.Add(1, tags);
    }

    public void Retry() => retry = true;

    bool retry;
}