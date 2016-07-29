namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CompositeViews.Endpoints;
    using EndpointControl;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.HeartbeatMonitoring.InternalMessages;
    using ServiceControl.Infrastructure;

    class HeartbeatsMonitor: Feature
    {
        public HeartbeatsMonitor()
        {
            EnableByDefault();
            RegisterStartupTask<StatusInitialiser>();
            RegisterStartupTask<HeartbeatMonitor>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");
            context.Container.ConfigureComponent<HeartbeatStatusProvider>(DependencyLifecycle.SingleInstance)
                        .ConfigureProperty(p => p.GracePeriod, settings.HeartbeatGracePeriod);
        }

        class HeartbeatMonitor : FeatureStartupTask
        {
            public IBus Bus { get; set; }

            public HeartbeatStatusProvider HeartbeatStatusProvider { get; set; }


            protected override void OnStart()
            {
                timer = new Timer(Refresh, null, 0, -1);
            }

            protected override void OnStop()
            {
                using (var manualResetEvent = new ManualResetEvent(false))
                {
                    timer.Dispose(manualResetEvent);

                    manualResetEvent.WaitOne();
                }
            }

            void Refresh(object _)
            {
                UpdateStatuses();

                try
                {
                    timer.Change((int)TimeSpan.FromSeconds(5).TotalMilliseconds, -1);
                }
                catch (ObjectDisposedException)
                {
                }
            }

            void UpdateStatuses()
            {
                var now = DateTime.UtcNow;

                foreach (var failingEndpoint in HeartbeatStatusProvider.GetPotentiallyFailedEndpoints(now))
                {
                    var id = DeterministicGuid.MakeId(failingEndpoint.Details.Name, failingEndpoint.Details.HostId.ToString());

                    Bus.SendLocal(new RegisterPotentiallyMissingHeartbeats
                    {
                        EndpointInstanceId = id,
                        DetectedAt = now,
                        LastHeartbeatAt = failingEndpoint.LastHeartbeatAt
                    });
                }
            }

            Timer timer;
        }

        class StatusInitialiser: FeatureStartupTask
        {
            readonly IDocumentStore store;
            readonly HeartbeatStatusProvider statusProvider;
            Task task;

            public StatusInitialiser(IDocumentStore store, HeartbeatStatusProvider statusProvider)
            {
                this.store = store;
                this.statusProvider = statusProvider;
            }
            protected override void OnStart()
            {
                task = Task.Factory.StartNew(Initialise);
            }

            protected override void OnStop()
            {
                if (task != null && !task.IsCompleted)
                {                   
                    task.Wait();
                }
            }

            void Initialise()
            {
                using (var session = store.OpenSession())
                {
                    session.Query<KnownEndpoint, KnownEndpointIndex>()
                        .Lazily(endpoints =>
                        {
                            foreach (var knownEndpoint in endpoints.Where(p => p.Monitored))
                            {
                                statusProvider.RegisterNewEndpoint(knownEndpoint.EndpointDetails);
                            }
                        });

                    session.Query<Heartbeat, HeartbeatsIndex>()
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
        }
    }
}
