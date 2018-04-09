namespace ServiceControl.Monitoring
{
    using ServiceControl.Contracts.Operations;

    public static class EndpointDetailsExtensions
    {
        public static EndpointInstanceId ToInstanceId(this EndpointDetails endpointDetails)
        {
            return new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId);
        }
    }
}