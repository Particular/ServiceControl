namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CompositeViews.Endpoints;
    using Contracts.Operations;
    using EndpointControl;
    using NServiceBus;
    using Raven.Client;

    public class HeartbeatsComputation : INeedInitialization
    {
        List<string> activeEndpoints = new List<string>();
        List<string> deadEndpoints = new List<string>();
 
        public HeartbeatsComputation()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public HeartbeatsComputation(IDocumentStore store)
        {
            this.store = store;
            Initialise(false);
        }

        public HeartbeatsStats Current
        {
            get
            {
                lock (locker)
                {
                    return new HeartbeatsStats(activeEndpoints.Count, deadEndpoints.Count);
                }
            }
        }

        public void Init()
        {
            Configure.Component<HeartbeatsComputation>(DependencyLifecycle.SingleInstance);
        }

        public HeartbeatsStats NewHeartbeatingEndpointDetected(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpointDetails.Name, endpointDetails.HostId);
                activeEndpoints.Add(endpointId);
                if (deadEndpoints.Contains(endpointId))
                {
                    deadEndpoints.Remove(endpointId);
                }
                return new HeartbeatsStats(activeEndpoints.Count, deadEndpoints.Count);
            }
        }

        public HeartbeatsStats NewEndpointDetected(EndpointDetails endpointDetails)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpointDetails.Name, endpointDetails.HostId);
                activeEndpoints.Add(endpointId);
                return new HeartbeatsStats(activeEndpoints.Count, deadEndpoints.Count);
            }
        }

        public HeartbeatsStats EndpointHeartbeatRestored(string endpoint, Guid hostId)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpoint, hostId);
                activeEndpoints.Add(endpointId);
                if (deadEndpoints.Contains(endpointId))
                {
                    deadEndpoints.Remove(endpointId);
                }
                return new HeartbeatsStats(activeEndpoints.Count, deadEndpoints.Count);
            }
        }

        public HeartbeatsStats EndpointFailedToHeartbeat(string endpoint, Guid hostId)
        {
            lock (locker)
            {
                var endpointId = string.Format("{0}-{1}", endpoint, hostId);
                deadEndpoints.Add(endpointId);
                if (activeEndpoints.Contains(endpointId))
                {
                    activeEndpoints.Remove(endpointId);
                }
                return new HeartbeatsStats(activeEndpoints.Count, deadEndpoints.Count);
            }
        }



        public HeartbeatsStats Reset()
        {
            lock (locker)
            {
                Initialise(true);
                return new HeartbeatsStats(activeEndpoints.Count, deadEndpoints.Count);
            }
        }

        void Initialise(bool waitForNonStale)
        {
            activeEndpoints.Clear();
            deadEndpoints.Clear();

            Action<IDocumentQueryCustomization> customization = c => { };

            if (waitForNonStale)
            {
                customization = c => c.WaitForNonStaleResultsAsOfLastWrite(TimeSpan.FromSeconds(50));
            }

            using (var session = store.OpenSession())
            {
                var total = 0;

                // Workaround to do Lazily Count, see https://groups.google.com/d/msg/ravendb/ptgTQbrPfzI/w9QJ0wdYkc4J
                // Raven v3 should support this natively, see http://issues.hibernatingrhinos.com/issue/RavenDB-1310

                session.Query<KnownEndpoint, KnownEndpointIndex>()
                    .Customize(customization)
                    .Where(endpoint => endpoint.MonitorHeartbeat)
                    .Lazily(heartbeats =>
                    {
                        foreach (var knownEndpoint in heartbeats)
                        {
                            deadEndpoints.Add(string.Format("{0}-{1}",knownEndpoint.Name, knownEndpoint.HostId));
                        }
                    });
                
                session.Query<Heartbeat, HeartbeatsIndex>()
                    .Customize(customization)
                    .Where(heartbeat => heartbeat.ReportedStatus == Status.Beating)
                    .Lazily(heartbeats =>
                    {
                        foreach (var heartbeat in heartbeats)
                        {
                            activeEndpoints.Add(string.Format("{0}-{1}", heartbeat.Endpoint, heartbeat.HostId));
                        }
                    });
                session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();

                // remove the endpoitns that are in the deadEndpoint list if they are currently active
                foreach (var endpointId in activeEndpoints)
                {
                    if (deadEndpoints.Contains(endpointId))
                    {
                        deadEndpoints.Remove(endpointId);
                    }
                }
            }
        }

        readonly object locker = new object();
        readonly IDocumentStore store;
      
        public class HeartbeatsStats
        {
            public HeartbeatsStats(int active, int dead)
            {
                Active = active;
                Dead = dead;
            }

            public readonly int Active;
            public readonly int Dead;
        }
    }
}
