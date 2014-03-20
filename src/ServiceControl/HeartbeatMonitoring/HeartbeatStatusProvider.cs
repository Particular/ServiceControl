namespace ServiceControl.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using Contracts.Operations;

    public class HeartbeatStatusProvider
    {
        List<string> activeEndpoints = new List<string>();
        List<string> deadEndpoints = new List<string>();
        readonly object locker = new object();

        public HeartbeatsComputation.HeartbeatsStats RegisterNewEndpoint(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpointDetails.Name, endpointDetails.HostId);
                deadEndpoints.Add(endpointId);
                return HeartbeatsStats();
            }
        }

        public HeartbeatsComputation.HeartbeatsStats RegisterHeartbeatingEndpoint(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpointDetails.Name, endpointDetails.HostId);
                activeEndpoints.Add(endpointId);
                if (deadEndpoints.Contains(endpointId))
                {
                    deadEndpoints.Remove(endpointId);
                }
                return HeartbeatsStats();
            }
        }

        public HeartbeatsComputation.HeartbeatsStats RegisterEndpointThatFailedToHeartbeat(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpointDetails.Name, endpointDetails.HostId);
                deadEndpoints.Add(endpointId);
                if (activeEndpoints.Contains(endpointId))
                {
                    activeEndpoints.Remove(endpointId);
                }
                return HeartbeatsStats();
            }
        }

        public HeartbeatsComputation.HeartbeatsStats RegisterEndpointWhoseHeartbeatIsRestored(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpointDetails.Name, endpointDetails.HostId);
                activeEndpoints.Add(endpointId);
                if (deadEndpoints.Contains(endpointId))
                {
                    deadEndpoints.Remove(endpointId);
                }
                return HeartbeatsStats();
            }
        }

        HeartbeatsComputation.HeartbeatsStats HeartbeatsStats()
        {
            return new HeartbeatsComputation.HeartbeatsStats(activeEndpoints.Count, deadEndpoints.Count);
        }
    }
}
