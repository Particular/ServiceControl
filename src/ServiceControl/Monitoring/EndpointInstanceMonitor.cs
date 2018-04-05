namespace ServiceControl.Monitoring
{
    using System;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EndpointControl.Contracts;
    using ServiceControl.HeartbeatMonitoring;
    using ServiceControl.Infrastructure.DomainEvents;

    public class EndpointInstanceMonitor
    {
        IDomainEvents domainEvents;

        public EndpointInstanceId Id { get; }
        private DateTime? lastSeen;
        private HeartbeatStatus status;

        public bool Monitored { get; private set; }

        public EndpointInstanceMonitor(EndpointInstanceId endpointInstanceId, bool monitored, IDomainEvents domainEvents)
        {
            Id = endpointInstanceId;
            Monitored = monitored;
            this.domainEvents = domainEvents;

            domainEvents.Raise(new NewEndpointDetected { DetectedAt = DateTime.UtcNow, Endpoint = Convert(Id)});
        }

        public void EnableMonitoring()
        {
            domainEvents.Raise(new MonitoringEnabledForEndpoint { Endpoint = Convert(Id) });
            Monitored = true;
        }

        public void DisableMonitoring()
        {
            domainEvents.Raise(new MonitoringDisabledForEndpoint { Endpoint = Convert(Id) });
            Monitored = false;
        }

        public void UpdateStatus(HeartbeatStatus newStatus, DateTime? latestTimestamp)
        {
            if (newStatus != status)
            {
                RaiseStateChangeEvents(newStatus, latestTimestamp);
            }

            lastSeen = latestTimestamp;
            status = newStatus;
        }

        private void RaiseStateChangeEvents(HeartbeatStatus newStatus, DateTime? latestTimestamp)
        {
            if (newStatus == HeartbeatStatus.Alive)
            {
                if (status == HeartbeatStatus.Unknown)
                {
                    // NOTE: If an endpoint starts randomly sending heartbeats we monitor it by default
                    // NOTE: This means we'll start monitoring endpoints sending heartbeats after a restart
                    Monitored = true;
                    domainEvents.Raise(new HeartbeatingEndpointDetected
                    {
                        Endpoint = Convert(Id),
                        DetectedAt = latestTimestamp ?? DateTime.UtcNow
                    });
                }
                else if (status == HeartbeatStatus.Dead && Monitored)
                {
                    domainEvents.Raise(new EndpointHeartbeatRestored
                    {
                        Endpoint = Convert(Id),
                        RestoredAt = latestTimestamp ?? DateTime.UtcNow
                    });
                }
            }
            else if (newStatus == HeartbeatStatus.Dead && Monitored)
            {
                domainEvents.Raise(new EndpointFailedToHeartbeat
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
                    stats.RecordActive();
                else
                    stats.RecordFailing();
            }
        }

        private EndpointDetails Convert(EndpointInstanceId endpointInstanceId)
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
                HeartbeatInformation = new HeartbeatInformation
                {
                    ReportedStatus = status == HeartbeatStatus.Alive ? Status.Beating : Status.Dead,
                    LastReportAt = lastSeen ?? DateTime.MinValue
                }
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
    }
}