namespace ServiceControl.Auditing.Metrics
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using NServiceBus.Transport;

    public record AuditMessageMetrics(MessageContext Context, Histogram<double> Duration) : IDisposable
    {
        public void Skipped() => result = "skipped";

        public void Success() => result = "success";

        public void Dispose()
        {
            var tags = AuditIngestionMetrics.GetMessageTags(Context.Headers);

            tags.Add("result", result);
            Duration.Record(sw.Elapsed.TotalSeconds, tags);
        }

        string result = "failed";

        readonly Stopwatch sw = Stopwatch.StartNew();
    }
}
