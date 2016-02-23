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
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.HeartbeatMonitoring.InternalMessages;
    using ServiceControl.Infrastructure;

    class HeartbeatsMonitor: Feature
    {
        public HeartbeatsMonitor()
        {
            EnableByDefault();
            RegisterStartupTask<StatusInitialiser>();
            RegisterStartupTask<HeartbeatMonitor>();
            RegisterStartupTask<HeartbeatsWriter>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<HeartbeatStatusProvider>(DependencyLifecycle.SingleInstance)
                        .ConfigureProperty(p => p.GracePeriod, Settings.HeartbeatGracePeriod);
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
                    timer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
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

        class HeartbeatsWriter : FeatureStartupTask
        {
            private readonly IDocumentStore store;
            public IBus Bus { get; set; }

            public HeartbeatStatusProvider HeartbeatStatusProvider { get; set; }

            public HeartbeatsWriter(IDocumentStore store)
            {
                this.store = store;
            }

            protected override void OnStart()
            {
                timer = new Timer(Refresh, null, TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);
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
                Sync();

                try
                {
                    timer.Change(TimeSpan.FromMinutes(2), Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                }
            }

            void Sync()
            {
                using (var session = store.OpenSession())
                {
                    var storedHeartbeats = session.Query<Heartbeat>().ToDictionary(h=> h.Id);
                    var inmemoryHeartbeats = HeartbeatStatusProvider.HeartbeatsPerInstance;

                    foreach (var memHeartbeat in inmemoryHeartbeats)
                    {
                        Heartbeat heartbeat;
                        if (storedHeartbeats.TryGetValue(memHeartbeat.Key, out heartbeat))
                        {
                            if (heartbeat.Disabled)
                            {
                                continue;
                            }

                            heartbeat.LastReportAt = memHeartbeat.Value.LastReportAt;
                            heartbeat.EndpointDetails = memHeartbeat.Value.EndpointDetails;
                            if (heartbeat.ReportedStatus == Status.Dead)
                            {
                                heartbeat.ReportedStatus = Status.Beating;
                                Bus.Publish(new EndpointHeartbeatRestored
                                {
                                    Endpoint = heartbeat.EndpointDetails,
                                    RestoredAt = heartbeat.LastReportAt
                                });
                            }
                        }
                        else
                        {
                            heartbeat = new Heartbeat
                            {
                                Id = memHeartbeat.Key,
                                ReportedStatus = Status.Beating,
                                LastReportAt = memHeartbeat.Value.LastReportAt,
                                EndpointDetails = memHeartbeat.Value.EndpointDetails
                            };

                            Bus.Publish(new HeartbeatingEndpointDetected
                            {
                                Endpoint = heartbeat.EndpointDetails,
                                DetectedAt = heartbeat.LastReportAt,
                            });
                        }

                        session.Store(heartbeat);

                        HeartbeatStatusProvider.RegisterHeartbeatingEndpoint(heartbeat.EndpointDetails, heartbeat.LastReportAt);
                    }

                    session.SaveChanges();
                }
            }

            Timer timer;
        }
    }
}
