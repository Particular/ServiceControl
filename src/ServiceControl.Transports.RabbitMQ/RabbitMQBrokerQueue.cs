#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System.Collections.Generic;
using NServiceBus.Transport.RabbitMQ.ManagementApi;
using ServiceControl.Transports.BrokerThroughput;

class RabbitMQBrokerQueue(Queue queue) : IBrokerQueue
{
    public string QueueName { get; } = queue.Name;

    public string SanitizedName => QueueName;

    public string? Scope => null;

    public List<string> EndpointIndicators { get; } = [];

    long? AckedMessages { get; set; } = queue.MessageStats?.Ack;

    long Baseline { get; set; } = queue.MessageStats?.Ack ?? 0;

    public long CalculateThroughputFrom(RabbitMQBrokerQueue newReading)
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
}