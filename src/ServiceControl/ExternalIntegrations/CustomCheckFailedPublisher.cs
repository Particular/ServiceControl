namespace ServiceControl.ExternalIntegrations
{
    using CustomCheckFailed = ServiceControl.Contracts.CustomCheckFailed;

    public class CustomCheckFailedPublisher : EventPublisher<Contracts.CustomChecks.CustomCheckFailed, CustomCheckFailed>
    {
        protected override CustomCheckFailed Convert(Contracts.CustomChecks.CustomCheckFailed message)
        {
            return new CustomCheckFailed
            {
                FailedAt = message.FailedAt,
                Category = message.Category,
                FailureReason = message.FailureReason,
                CustomCheckId = message.CustomCheckId,
                Host = message.OriginatingEndpoint.Host,
                HostId = message.OriginatingEndpoint.HostId,
                EndpointName = message.OriginatingEndpoint.Name
            };
        }
    }
}