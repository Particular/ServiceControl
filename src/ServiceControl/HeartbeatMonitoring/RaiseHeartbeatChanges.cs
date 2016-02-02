namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;

    public class RaiseHeartbeatChanges :
        IHandleMessages<HeartbeatStatusChanged>,
        IHandleMessages<NewEndpointDetected>
    {

        public HeartbeatStatusProvider StatusProvider { get; set; }

        public Task Handle(HeartbeatStatusChanged message, IMessageHandlerContext context)
        {
            return PublishUpdate(StatusProvider.GetHeartbeatsStats(), context);
        }

        public Task Handle(NewEndpointDetected message, IMessageHandlerContext context)
        {
            //this call is non intuitive, we just call it since endpoints without the heartbeat plugin installed should count as "failing"
            // we need to revisit the requirements for this
            return PublishUpdate(StatusProvider.RegisterNewEndpoint(message.Endpoint), context);
        }

        Task PublishUpdate(HeartbeatsStats stats, IMessageHandlerContext context)
        {
            return context.Publish(new HeartbeatsUpdated
            {
                Active = stats.Active,
                Failing = stats.Dead,
            });
        }
    }
}