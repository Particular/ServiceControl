namespace ServiceControl.ExternalIntegrations
{
    using ServiceControl.Contracts;

    public class CustomCheckSucceededPublisher : EventPublisher<Contracts.CustomChecks.CustomCheckSucceeded, CustomCheckSucceeded>
    {
        protected override CustomCheckSucceeded Convert(Contracts.CustomChecks.CustomCheckSucceeded message)
        {
            return new CustomCheckSucceeded
            {
                SucceededAt = message.SucceededAt,
                Category = message.Category,
                CustomCheckId = message.CustomCheckId,
                Host = message.OriginatingEndpoint.Host,
                HostId = message.OriginatingEndpoint.HostId,
                EndpointName = message.OriginatingEndpoint.Name
            };
        }
    }
}