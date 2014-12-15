namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using CompositeViews.Endpoints;
    using EndpointControl;
    using NServiceBus;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class HeartbeatStatusInitializer : INeedInitialization, IWantToRunWhenBusStartsAndStops
    {

        public HeartbeatStatusInitializer()
        {
            // Need this because INeedInitialization does not use DI instead use Activator.CreateInstance
        }

        public HeartbeatStatusInitializer(IDocumentStore store, HeartbeatStatusProvider statusProvider)
        {
            this.store = store;
            this.statusProvider = statusProvider;
        }


        public void Init()
        {
            Configure.Component<HeartbeatStatusInitializer>(DependencyLifecycle.SingleInstance);
            Configure.Component<HeartbeatStatusProvider>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(p => p.GracePeriod, Settings.HeartbeatGracePeriod);
        }

        public void Start()
        {
            Initialise();
        }

        public void Stop()
        {

        }


        void Initialise()
        {
            Action<IDocumentQueryCustomization> customization = c => { };

            using (var session = store.OpenSession())
            {
                session.Query<KnownEndpoint, KnownEndpointIndex>()
                    .Customize(customization)
                    .Lazily(endpoints =>
                    {
                        foreach (var knownEndpoint in endpoints.Where(p => p.Monitored))
                        {
                            statusProvider.RegisterNewEndpoint(knownEndpoint.EndpointDetails);
                        }
                    });

                session.Query<Heartbeat, HeartbeatsIndex>()
                    .Customize(customization)
                    .Lazily(heartbeats =>
                    {
                        foreach (var heartbeat in heartbeats)
                        {
                            if (heartbeat.Disabled)
                            {
                                statusProvider.DisableMonitoring(heartbeat.EndpointDetails);
                                continue;
                            }

                            statusProvider.EnableMonitoring(heartbeat.EndpointDetails);

                            if (heartbeat.ReportedStatus == Status.Beating)
                            {
                                statusProvider.RegisterHeartbeatingEndpoint(heartbeat.EndpointDetails, heartbeat.LastReportAt);
                            }
                            else
                            {
                                statusProvider.RegisterEndpointThatFailedToHeartbeat(heartbeat.EndpointDetails);
                            }
                        }
                    });
                session.Advanced.Eagerly.ExecuteAllPendingLazyOperations();
            }
        }

        readonly IDocumentStore store;
        readonly HeartbeatStatusProvider statusProvider;

    }
}
