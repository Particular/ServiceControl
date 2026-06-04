namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Metrics;
    using Transports;

    [Handler]
    public class EndpointMetadataReportHandler(IProvideQueueLength queueLengthProvider) : IHandleMessages<EndpointMetadataReport>
    {
        public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(endpointName, message.LocalAddress));

            return Task.CompletedTask;
        }
    }
}