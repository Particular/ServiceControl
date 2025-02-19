namespace ServiceControl.Infrastructure;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using NServiceBus;
using OpenTelemetry.Metrics;

static class Telemetry
{
    const string MeterName = "Particular.ServiceControl.Audit";
    public static readonly Meter Meter = new(MeterName, "0.1.0");

    public static string CreateInstrumentName(string instrumentNamespace, string instrumentName) => $"sc.audit.{instrumentNamespace}.{instrumentName}".ToLower();

    public static void AddAuditIngestionMeters(this MeterProviderBuilder builder) => builder.AddMeter(MeterName);

    public static TagList GetIngestedMessageTags(IDictionary<string, string> headers, ReadOnlyMemory<byte> body)
    {
        var tags = new TagList { { "messaging.message.body.size", body.Length } };

        if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType))
        {
            tags.Add("messaging.message.type", messageType);
        }

        return tags;
    }
}