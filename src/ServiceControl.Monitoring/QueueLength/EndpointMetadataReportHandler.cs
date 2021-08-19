namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Metrics;
    using Transports;

    public class EndpointMetadataReportHandler : IHandleMessages<EndpointMetadataReport>
    {
        public EndpointMetadataReportHandler(IProvideQueueLength queueLengthProvider)
        {
            this.queueLengthProvider = queueLengthProvider;
        }

        public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(endpointName, message.LocalAddress));

            return Task.CompletedTask;
        }

        IProvideQueueLength queueLengthProvider;
    }
}