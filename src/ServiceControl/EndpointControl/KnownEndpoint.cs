namespace ServiceControl.EndpointControl
{
    using System;
    using ServiceControl.Contracts.Operations;

    public class KnownEndpoint
    {
        public KnownEndpoint()
        {
            MonitorHeartbeat = true;
        }

        public Guid Id { get; set; }
        public string HostDisplayName { get; set; }
        public bool MonitorHeartbeat { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public bool HasTemporaryId{ get; set; }
    }
}