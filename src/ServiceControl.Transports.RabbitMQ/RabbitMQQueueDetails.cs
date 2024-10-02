#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System.Collections.Generic;
using System.Text.Json;
using ServiceControl.Transports.BrokerThroughput;

public class RabbitMQBrokerQueueDetails(JsonElement token) : IBrokerQueue
{
    public string QueueName { get; } = token.GetProperty("name").GetString()!;
    public string SanitizedName => QueueName;
    public string Scope => VHost;
    public string VHost { get; } = token.GetProperty("vhost").GetString()!;
    public List<string> EndpointIndicators { get; } = [];
    long? AckedMessages { get; set; } = FromToken(token);
    long Baseline { get; set; } = FromToken(token) ?? 0;

    public long CalculateThroughputFrom(RabbitMQBrokerQueueDetails newReading)
    {
        var newlyAckedMessages = 0L;
        if (newReading.AckedMessages is null)
        {
            return newlyAckedMessages;
        }

        if (newReading.AckedMessages.Value >= Baseline)
        {
            newlyAckedMessages = newReading.AckedMessages.Value - Baseline;
            AckedMessages += newlyAckedMessages;
        }
        Baseline = newReading.AckedMessages.Value;

        return newlyAckedMessages;
    }

    static long? FromToken(JsonElement jsonElement) =>
        jsonElement.TryGetProperty("message_stats", out var stats) && stats.TryGetProperty("ack", out var val)
            ? val.GetInt64()
            : null;
}