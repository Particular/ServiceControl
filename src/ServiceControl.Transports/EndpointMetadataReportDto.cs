namespace ServiceControl.Transports
{
    public class EndpointMetadataReportDto
    {
        public EndpointMetadataReportDto(string localAddress)
        {
            LocalAddress = localAddress;
        }

        public string LocalAddress { get; set; }
    }
}