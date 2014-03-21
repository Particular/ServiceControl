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
                GetEndpoint(endpointDetails);

                return GetHeartbeatsStats();
            }
        }

        public HeartbeatsStats RegisterHeartbeatingEndpoint(EndpointDetails endpointDetails,DateTime timeOfHeartbeat)
        {
            lock (locker)
            {
                var existingEndpoint = GetEndpoint(endpointDetails);


                existingEndpoint.Active = true;
                existingEndpoint.TimeOfLastHeartbeat = timeOfHeartbeat;

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

        public HeartbeatsStats RegisterEndpointWhoseHeartbeatIsRestored(EndpointDetails endpointDetails, DateTime timeOfHeartbeat)
        {
            return RegisterHeartbeatingEndpoint(endpointDetails,timeOfHeartbeat);
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

        public IEnumerable<PotentiallyFailedEndpoint> GetPotentiallyFailedEndpoints(DateTime time)
        {
            lock (locker)
            {
                return endpoints.Where(e =>HasPassedTheGracePeriod(time,e))
                    .Select(e => new PotentiallyFailedEndpoint
                {
                    Details = new EndpointDetails { Host = e.Host,HostId = e.HostId,Name = e.Name},
                    LastHeartbeatAt = e.TimeOfLastHeartbeat.Value

                }).ToList();
            }
        }

        bool HasPassedTheGracePeriod(DateTime time,HeartbeatingEndpoint heartbeatingEndpoint)
        {
            if (!heartbeatingEndpoint.TimeOfLastHeartbeat.HasValue)
            {
                return false;
            }

            if (!heartbeatingEndpoint.Active)
            {
                return false;
            }


            var timeSinceLastHeartbeat = time - heartbeatingEndpoint.TimeOfLastHeartbeat;

            return timeSinceLastHeartbeat >= GracePeriod;
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



        public class PotentiallyFailedEndpoint
        {
            public DateTime LastHeartbeatAt { get; set; }
            public EndpointDetails Details { get; set; }

        }

        public TimeSpan GracePeriod { get; set; }
    }
}
