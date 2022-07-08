namespace ServiceControl.EndpointControl.Contracts
{
    using Infrastructure.DomainEvents;
    using ServiceControl.Contracts.Operations;

    public class EndpointsDetectedFromIngestion : IDomainEvent
    {
        public EndpointDetails[] Endpoints { get; set; }
    }
}