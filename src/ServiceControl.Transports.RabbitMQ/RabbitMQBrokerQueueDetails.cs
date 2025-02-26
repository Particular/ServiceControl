#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System.Collections.Generic;
using ServiceControl.Transports.BrokerThroughput;
using NServiceBus.Transport.RabbitMQ.ManagementApi;

class RabbitMQBrokerQueueDetails(Queue queue) : IBrokerQueue
{
    public string QueueName { get; } = queue.Name;
    public string SanitizedName => QueueName;
    public string? Scope => null;
    //public string VHost { get; } = queue.Vhost;
    public List<string> EndpointIndicators { get; } = [];
    long? AckedMessages { get; set; } = queue.MessageStats?.Ack;
    long Baseline { get; set; } = queue.MessageStats?.Ack ?? 0;

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
}