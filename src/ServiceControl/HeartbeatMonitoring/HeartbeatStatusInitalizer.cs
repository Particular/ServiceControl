namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using CompositeViews.Endpoints;
    using EndpointControl;
    using NServiceBus;
    using Raven.Client;

    public class HeartbeatStatusInitalizer : INeedInitialization
    {
 
        public HeartbeatStatusInitalizer()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public HeartbeatStatusInitalizer(IDocumentStore store,HeartbeatStatusProvider statusProvider)
        {
            this.store = store;
            this.statusProvider = statusProvider;

            Initialise();
        }

     
        public void Init()
        {
            Configure.Component<HeartbeatStatusInitalizer>(DependencyLifecycle.SingleInstance);
            Configure.Component<HeartbeatStatusProvider>(DependencyLifecycle.SingleInstance);
        }

     
        void Initialise()
        {
            Action<IDocumentQueryCustomization> customization = c => { };

            using (var session = store.OpenSession())
            {
                // Workaround to do Lazily Count, see https://groups.google.com/d/msg/ravendb/ptgTQbrPfzI/w9QJ0wdYkc4J
                // Raven v3 should support this natively, see http://issues.hibernatingrhinos.com/issue/RavenDB-1310

                session.Query<KnownEndpoint, KnownEndpointIndex>()
                    .Customize(customization)
                    .Lazily(heartbeats =>
                    {
                        foreach (var knownEndpoint in heartbeats)
                        {
                            if (knownEndpoint.MonitorHeartbeat)
                            {
                                statusProvider.EnableMonitoring(knownEndpoint.EndpointDetails);
                            }
                            else
                            {
                                statusProvider.DisableMonitoring(knownEndpoint.EndpointDetails);
                            }

                        }
                    });
                
                session.Query<Heartbeat, HeartbeatsIndex>()
                    .Customize(customization)
                    .Lazily(heartbeats =>
                    {
                        foreach (var heartbeat in heartbeats)
                        {
                            //we initalize all as "dead" since this happens when we startup so we can't assume that they are still running
                            statusProvider.RegisterEndpointThatFailedToHeartbeat(heartbeat.EndpointDetails);
                        }
                    });
                session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();
            }
        }

        readonly IDocumentStore store;
        readonly HeartbeatStatusProvider statusProvider;
    }
}
