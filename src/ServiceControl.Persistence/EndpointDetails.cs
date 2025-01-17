namespace ServiceControl.Operations
{
    using System;
    using ServiceControl.Persistence.Infrastructure;

    public class EndpointDetails
    {
        public string Name { get; set; }

        public Guid HostId { get; set; }

        public string Host { get; set; }

        public Guid GetDeterministicId() => DeterministicGuid.MakeId(Name, HostId.ToString());
    }
}