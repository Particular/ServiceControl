namespace ServiceControl.Monitoring
{
    using CompositeViews.Endpoints;
    using Contracts.Operations;

    class EndpointInstance
    {
        public EndpointInstance(EndpointInstanceId endpointInstanceId)
        {
            Id = endpointInstanceId;
        }

        public EndpointInstanceId Id { get; }


        static EndpointDetails Convert(EndpointInstanceId endpointInstanceId)
        {
            return new EndpointDetails
            {
                Host = endpointInstanceId.HostName,
                HostId = endpointInstanceId.HostGuid,
                Name = endpointInstanceId.LogicalName
            };
        }

        public KnownEndpointsView GetKnownView()
        {
            return new KnownEndpointsView
            {
                Id = Id.UniqueId,
                HostDisplayName = Id.HostName,
                EndpointDetails = Convert(Id)
            };
        }
    }
}