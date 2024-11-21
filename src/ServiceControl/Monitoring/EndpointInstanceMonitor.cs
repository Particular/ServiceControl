namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Contracts.HeartbeatMonitoring;
    using EndpointControl.Contracts;
    using Infrastructure.DomainEvents;
    using NLog.Fluent;
    using NServiceBus.Logging;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class EndpointInstanceMonitor
    {
        public EndpointInstanceMonitor(EndpointInstanceId endpointInstanceId, bool monitored, IDomainEvents domainEvents)
        {
            Id = endpointInstanceId;
            Monitored = monitored;
            this.domainEvents = domainEvents;
        }

        public EndpointInstanceId Id { get; }

        public bool Monitored { get; private set; }

        public async Task EnableMonitoring()
        {
            await domainEvents.Raise(new MonitoringEnabledForEndpoint { Endpoint = Convert(Id) });
            Monitored = true;
        }

        public async Task DisableMonitoring()
        {
            await domainEvents.Raise(new MonitoringDisabledForEndpoint { Endpoint = Convert(Id) });
            Monitored = false;
        }

        public async Task UpdateStatus(HeartbeatStatus newStatus, DateTime? latestTimestamp)
        {
            if (newStatus != status)
            {
                await RaiseStateChangeEvents(newStatus, latestTimestamp);
                Log.DebugFormat("Endpoint {0} status updated from {1} to {2}", Id.LogicalName, status, newStatus);
            }

            lastSeen = latestTimestamp;
            status = newStatus;
        }

        async Task RaiseStateChangeEvents(HeartbeatStatus newStatus, DateTime? latestTimestamp)
        {
            if (newStatus == HeartbeatStatus.Alive)
            {
                if (status == HeartbeatStatus.Unknown)
                {
                    // NOTE: If an endpoint starts randomly sending heartbeats we monitor it by default
                    // NOTE: This means we'll start monitoring endpoints sending heartbeats after a restart
                    Monitored = true;
                    await domainEvents.Raise(new HeartbeatingEndpointDetected
                    {
                        Endpoint = Convert(Id),
                        DetectedAt = latestTimestamp ?? DateTime.UtcNow
                    });
                }
                else if (status == HeartbeatStatus.Dead && Monitored)
                {
                    await domainEvents.Raise(new EndpointHeartbeatRestored
                    {
                        Endpoint = Convert(Id),
                        RestoredAt = latestTimestamp ?? DateTime.UtcNow
                    });
                }
            }
            else if (newStatus == HeartbeatStatus.Dead && Monitored)
            {
                await domainEvents.Raise(new EndpointFailedToHeartbeat
                {
                    Endpoint = Convert(Id),
                    DetectedAt = DateTime.UtcNow,
                    LastReceivedAt = latestTimestamp ?? DateTime.MinValue
                });
            }
        }

        public void AddTo(EndpointMonitoringStats stats)
        {
            if (Monitored)
            {
                if (status == HeartbeatStatus.Alive)
                {
                    stats.RecordActive();
                }
                else
                {
                    stats.RecordFailing();
                }
            }
        }

        static EndpointDetails Convert(EndpointInstanceId endpointInstanceId)
        {
            return new EndpointDetails
            {
                Host = endpointInstanceId.HostName,
                HostId = endpointInstanceId.HostGuid,
                Name = endpointInstanceId.LogicalName
            };
        }

        public EndpointsView GetView()
        {
            return new EndpointsView
            {
                Id = Id.UniqueId,
                Name = Id.LogicalName,
                HostDisplayName = Id.HostName,
                Monitored = Monitored,
                MonitorHeartbeat = Monitored,
                SupportsHeartbeats = Id.SupportsHeartbeats,

                HeartbeatInformation = lastSeen.HasValue ? new HeartbeatInformation
                {
                    ReportedStatus = status == HeartbeatStatus.Alive ? HeartbeatMonitoringStatus.Beating : HeartbeatMonitoringStatus.Dead,
                    LastReportAt = lastSeen.Value
                } : null
            };
        }

        public KnownEndpointsView GetKnownView()
        {
            return new KnownEndpointsView
            {
                Id = Id.UniqueId,
                HostDisplayName = Id.HostName,
                EndpointDetails = Convert(Id)
            };
        }

        static readonly ILog Log = LogManager.GetLogger<EndpointInstanceMonitor>();

        IDomainEvents domainEvents;
        DateTime? lastSeen;
        HeartbeatStatus status;
    }
}
