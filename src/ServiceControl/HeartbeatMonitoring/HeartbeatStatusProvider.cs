namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.Plugin.Heartbeat.Messages;

    public class HeartbeatStatusProvider
    {
        public HeartbeatsStats RegisterNewEndpoint(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                // TODO: Create Heartbeat Document?
                //GetEndpoint(endpointDetails);

                return GetHeartbeatsStatsNoLock();
            }
        }

        public HeartbeatsStats RegisterHeartbeatingEndpoint(EndpointDetails endpointDetails, DateTime timeOfHeartbeat)
        {
            lock (locker)
            {
                // TODO: Register Heartbeating Endpoint
                //var existingEndpoint = GetEndpoint(endpointDetails);


                //existingEndpoint.Active = true;
                //existingEndpoint.TimeOfLastHeartbeat = timeOfHeartbeat;

                return GetHeartbeatsStatsNoLock();
            }
        }

        public HeartbeatsStats RegisterEndpointThatFailedToHeartbeat(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                // TODO: Register Dead Endpoint
                //var existingEndpoint = GetEndpoint(endpointDetails);


                //existingEndpoint.Active = false;

                return GetHeartbeatsStatsNoLock();
            }
        }

        public HeartbeatsStats RegisterEndpointWhoseHeartbeatIsRestored(EndpointDetails endpointDetails, DateTime timeOfHeartbeat)
        {
            return RegisterHeartbeatingEndpoint(endpointDetails, timeOfHeartbeat);
        }

        public HeartbeatsStats GetHeartbeatsStats()
        {
            lock (locker)
            {
                return GetHeartbeatsStatsNoLock();
            }
        }

        public HeartbeatsStats EnableMonitoring(EndpointDetails endpoint)
        {
            lock (locker)
            {
                Heartbeat heartbeat;
                if (TryGetHeartbeat(endpoint, out heartbeat))
                {
                    heartbeat.Disabled = false;
                }
                return GetHeartbeatsStatsNoLock();
            }
        }

        public HeartbeatsStats DisableMonitoring(EndpointDetails endpoint)
        {
            lock (locker)
            {
                Heartbeat heartbeat;
                if (TryGetHeartbeat(endpoint, out heartbeat))
                {
                    heartbeat.Disabled = true;
                }
                return GetHeartbeatsStatsNoLock();
            }
        }

        public IEnumerable<PotentiallyFailedEndpoint> GetPotentiallyFailedEndpoints(DateTime time)
        {
            lock (locker)
            {
                return heartbeatsPerInstance.Values.Where(e => HasPassedTheGracePeriod(time, e))
                    .Select(e => new PotentiallyFailedEndpoint
                    {
                        Details = e.EndpointDetails,
                        LastHeartbeatAt = e.LastReportAt
                    }).ToList();
            }
        }

        HeartbeatsStats GetHeartbeatsStatsNoLock()
        {
            var heartbeats = heartbeatsPerInstance.Values.Where(x => x.Disabled == false).ToArray();

            return new HeartbeatsStats(heartbeats.Count(x => x.ReportedStatus == Status.Beating), heartbeats.Count(x => x.ReportedStatus == Status.Dead));
        }

        bool HasPassedTheGracePeriod(DateTime time, Heartbeat heartbeat)
        {
            if (heartbeat.Disabled)
            {
                return false;
            }

            var timeSinceLastHeartbeat = time - heartbeat.LastReportAt;

            return timeSinceLastHeartbeat >= GracePeriod;
        }

        bool TryGetHeartbeat(EndpointDetails endpointDetails, out Heartbeat heartbeat)
        {
            var key = GetHeartbeatId(endpointDetails);

            return heartbeatsPerInstance.TryGetValue(key, out heartbeat);
        }



        //HeartbeatingEndpoint GetEndpoint(EndpointDetails endpointDetails)
        //{
        //    var existingEndpoint = TryFindEndpoint(endpointDetails);
        //    if (existingEndpoint == null)
        //    {
        //        existingEndpoint = new HeartbeatingEndpoint
        //        {
        //            Host = endpointDetails.Host,
        //            HostId = endpointDetails.HostId,
        //            Name = endpointDetails.Name
        //        };

        //        endpoints.Add(existingEndpoint);
        //    }
        //    else
        //    {
        //        if (existingEndpoint.HostId == Guid.Empty && endpointDetails.HostId != Guid.Empty)
        //        {
        //            existingEndpoint.HostId = endpointDetails.HostId;
        //        }
        //    }
        //    return existingEndpoint;
        //}

        //HeartbeatingEndpoint TryFindEndpoint(EndpointDetails endpointDetails)
        //{
        //    if (endpointDetails.HostId == Guid.Empty)
        //    {
        //        // Try to match existing ones on host and machine if no host id is present
        //        return endpoints.Where(e => e.Host == endpointDetails.Host && e.Name == endpointDetails.Name)
        //            .OrderBy(e => e.HostId) // This is a hack because of Issue #448
        //            .FirstOrDefault();
        //    }

        //    //try to get an exact match
        //    var existingEndpoint = endpoints.SingleOrDefault(e => e.HostId == endpointDetails.HostId && e.Name == endpointDetails.Name);

        //    if (existingEndpoint != null)
        //    {
        //        return existingEndpoint;
        //    }

        //    //try to match on existing ones without host IDs
        //    return endpoints.SingleOrDefault(e =>
        //        e.HostId == Guid.Empty &&
        //        e.Host == endpointDetails.Host && e.Name == endpointDetails.Name);

        //}

        readonly object locker = new object();

        //List<HeartbeatingEndpoint> endpoints = new List<HeartbeatingEndpoint>();
        ConcurrentDictionary<Guid, Heartbeat> heartbeatsPerInstance = new ConcurrentDictionary<Guid, Heartbeat>();


        public class PotentiallyFailedEndpoint
        {
            public DateTime LastHeartbeatAt { get; set; }
            public EndpointDetails Details { get; set; }

        }

        public TimeSpan GracePeriod { get; set; }

        public void UpdateHeartbeat(EndpointDetails endpoint, DateTime lastExecuted)
        {
            var heartbeatId = GetHeartbeatId(endpoint);

            heartbeatsPerInstance.AddOrUpdate(heartbeatId,
                newId => CreateNewEndpointHeartbeat(newId, endpoint, lastExecuted),
                (currentId, currentHeartbeat) => UpdateExistingHeartbeat(lastExecuted, currentHeartbeat));
        }

        private Guid GetHeartbeatId(EndpointDetails details)
        {
            return DeterministicGuid.MakeId(details.Name, details.HostId.ToString());
        }

        static Heartbeat UpdateExistingHeartbeat(DateTime newBeatTime, Heartbeat currentHeartbeat)
        {
            if (currentHeartbeat.LastReportAt >= newBeatTime)
            {
                return currentHeartbeat;
            }

            currentHeartbeat.LastReportAt = newBeatTime;

            if (currentHeartbeat.ReportedStatus == Status.Dead)
            {
                currentHeartbeat.ReportedStatus = Status.Beating;

                // TODO: Raise Notification
                // TODO: Persist
            }

            return currentHeartbeat;
        }

        public IDictionary<Guid, Heartbeat> HeartbeatsPerInstance
        {
            get { return heartbeatsPerInstance; }
        }

        static Heartbeat CreateNewEndpointHeartbeat(Guid newId, EndpointDetails endpoint, DateTime lastExecutedAt)
        {
            // TODO: Raise new heartbeat event?
            // TODO: Persist new heartbeat?

            return new Heartbeat
            {
                Id = newId,
                Disabled = false,
                EndpointDetails = endpoint,
                LastReportAt = lastExecutedAt,
                ReportedStatus = Status.Beating
            };
        }
    }
}