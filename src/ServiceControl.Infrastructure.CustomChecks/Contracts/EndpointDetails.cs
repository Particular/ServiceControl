namespace ServiceControl.Contracts.CustomChecks
{
    using System;

    public class EndpointDetails
    {
        public string Name { get; set; }

        public Guid HostId { get; set; }

        public string Host { get; set; }
    }
}