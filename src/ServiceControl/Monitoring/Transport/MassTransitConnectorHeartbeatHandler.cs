namespace ServiceControl.Monitoring;

using System.Threading.Tasks;
using Connector.MassTransit;
using HeartbeatMonitoring;
using NServiceBus;

class MassTransitConnectorHeartbeatHandler(MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus) : IHandleMessages<MassTransitConnectorHeartbeat>
{
    public Task Handle(MassTransitConnectorHeartbeat message, IMessageHandlerContext context)
    {
        if (connectorHeartbeatStatus.LastHeartbeat == null || message.SentDateTimeOffset > connectorHeartbeatStatus.LastHeartbeat.SentDateTimeOffset)
        {
            connectorHeartbeatStatus.Update(message);
        }

        return Task.CompletedTask;
    }
}