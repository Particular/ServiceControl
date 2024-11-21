namespace ServiceControl.Monitoring
{
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    public static class EndpointDetailsExtensions
    {
        public static EndpointInstanceId ToInstanceId(this EndpointDetails endpointDetails)
        {
            return new EndpointInstanceId(endpointDetails.Name, endpointDetails.Host, endpointDetails.HostId, true);
        }
    }
}