namespace ServiceControl.Audit.Auditing.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using NServiceBus.Transport;

public record MessageMetrics(MessageContext Context, Histogram<double> Duration) : IDisposable
{
    public void Skipped() => result = "skipped";

    public void Success() => result = "success";

    public void Dispose()
    {
        var tags = IngestionMetrics.GetMessageTags(Context.Headers);

        tags.Add("result", result);
        Duration.Record(sw.ElapsedMilliseconds, tags);
    }

    string result = "failed";

    readonly Stopwatch sw = Stopwatch.StartNew();
}