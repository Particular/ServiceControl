namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;

    public class HeartbeatStatusProvider
    {
        public HeartbeatsStats RegisterNewEndpoint(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var existingEndpoint = GetEndpoint(endpointDetails);

                return GetHeartbeatsStats();
            }
        }

        public HeartbeatsStats RegisterHeartbeatingEndpoint(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var existingEndpoint = GetEndpoint(endpointDetails);


                existingEndpoint.Active = true;

                return GetHeartbeatsStats();
            }
        }

       

        public HeartbeatsStats RegisterEndpointThatFailedToHeartbeat(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var existingEndpoint = GetEndpoint(endpointDetails);


                existingEndpoint.Active = false;

                return GetHeartbeatsStats();
            }
        }

        public HeartbeatsStats RegisterEndpointWhoseHeartbeatIsRestored(EndpointDetails endpointDetails)
        {
            return RegisterHeartbeatingEndpoint(endpointDetails);
        }

        public HeartbeatsStats GetHeartbeatsStats()
        {
            return new HeartbeatsStats(endpoints.Count(e => !e.MonitoringDisabled && e.Active), endpoints.Count(e =>!e.MonitoringDisabled &&  !e.Active));
        }

      

        public HeartbeatsStats EnableMonitoring(EndpointDetails endpoint)
        {
            lock (locker)
            {
                var existingEndpoint = GetEndpoint(endpoint);
                existingEndpoint.MonitoringDisabled = false;
                return GetHeartbeatsStats();
            }
        }

        public HeartbeatsStats DisableMonitoring(EndpointDetails endpoint)
        {
            lock (locker)
            {
                var existingEndpoint = GetEndpoint(endpoint);
                existingEndpoint.MonitoringDisabled = true;
                return GetHeartbeatsStats();
            }
        }

        HeartbeatingEndpoint GetEndpoint(EndpointDetails endpointDetails)
        {
            var existingEndpoint = TryFindEndpoint(endpointDetails);
            if (existingEndpoint == null)
            {
                existingEndpoint = new HeartbeatingEndpoint
                {
                    Host = endpointDetails.Host,
                    HostId = endpointDetails.HostId,
                    Name = endpointDetails.Name
                };

                endpoints.Add(existingEndpoint);
            }
            else
            {
                if (existingEndpoint.HostId != Guid.Empty && endpointDetails.HostId != Guid.Empty)
                {
                    existingEndpoint.HostId = endpointDetails.HostId;
                }
            }
            return existingEndpoint;
        }

        HeartbeatingEndpoint TryFindEndpoint(EndpointDetails endpointDetails)
        {
            if (endpointDetails.HostId == Guid.Empty)
            {
                // Try to match existing ones on host and machine if no host id is present
                return endpoints.SingleOrDefault(e => e.Host == endpointDetails.Host && e.Name == endpointDetails.Name);
            }

            //try to get an exact match
            var existingEndpoint = endpoints.SingleOrDefault(e => e.HostId == endpointDetails.HostId && e.Name == endpointDetails.Name);

            if (existingEndpoint != null)
            {
                return existingEndpoint;
            }

            //try to match on existing ones without host IDs
            return endpoints.SingleOrDefault(e =>
                e.HostId == Guid.Empty &&
                e.Host == endpointDetails.Host && e.Name == endpointDetails.Name);

        }

        readonly object locker = new object();

        List<HeartbeatingEndpoint> endpoints = new List<HeartbeatingEndpoint>();
    }
}
