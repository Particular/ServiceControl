namespace ServiceControl.Monitoring
{
    class EndpointInstance
    {
        public EndpointInstance(EndpointInstanceId endpointInstanceId)
        {
            Id = endpointInstanceId;
        }

        public EndpointInstanceId Id { get; }
    }
}