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
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(builder => builder.Build<StatusInitialiser>());
            context.RegisterStartupTask(builder => builder.Build<HeartbeatMonitor>());
            context.Container.ConfigureComponent<HeartbeatStatusProvider>(DependencyLifecycle.SingleInstance)
                        .ConfigureProperty(p => p.GracePeriod, Settings.HeartbeatGracePeriod);
        }

        class HeartbeatMonitor : FeatureStartupTask
        {
            public IBusSession BusSession { get; set; }

            public HeartbeatStatusProvider HeartbeatStatusProvider { get; set; }


            protected override Task OnStart(IBusSession session)
            {
                timer = new Timer(Refresh, null, 0, -1);
                Task.FromResult(0);
            }

            protected override Task OnStop(IBusSession session)
            {
                using (var manualResetEvent = new ManualResetEvent(false))
                {
                    timer.Dispose(manualResetEvent);

                    manualResetEvent.WaitOne();
                }

                Task.FromResult(0);
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

                    BusSession.SendLocal(new RegisterPotentiallyMissingHeartbeats
                    {
                        EndpointInstanceId = id,
                        DetectedAt = now,
                        LastHeartbeatAt = failingEndpoint.LastHeartbeatAt
                    }).GetAwaiter().GetResult();
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
            protected override Task OnStart(IBusSession session)
            {
                task = Task.Factory.StartNew(Initialise);
                return Task.FromResult(0);
            }

            protected override Task OnStop(IBusSession session)
            {
                if (task != null && !task.IsCompleted)
                {                   
                    task.Wait();
                }

                return Task.FromResult(0);
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
