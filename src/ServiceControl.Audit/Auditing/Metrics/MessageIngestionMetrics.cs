namespace ServiceControl.Audit.Auditing.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using EndpointPlugin.Messages.SagaState;
using NServiceBus;
using NServiceBus.Transport;

public record MessageIngestionMetrics(MessageContext Message, Histogram<double> Duration) : IDisposable
{
    public void Skipped() => result = "skipped";

    public void Success() => result = "success";

    public void Dispose()
    {
        var tags = GetTags(Message);

        tags.Add("result", result);
        Duration.Record(sw.ElapsedMilliseconds, tags);
    }

    static TagList GetTags(MessageContext messageContext)
    {
        var tags = new TagList();

        if (messageContext.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType))
        {
            tags.Add("message.category", messageType == SagaUpdateMessageType ? "saga-update" : "audit-message");
        }
        else
        {
            tags.Add("message.category", "control-message");
        }

        return tags;
    }

    string result = "failed";

    readonly Stopwatch sw = Stopwatch.StartNew();

    static readonly string SagaUpdateMessageType = typeof(SagaUpdatedMessage).FullName;
}