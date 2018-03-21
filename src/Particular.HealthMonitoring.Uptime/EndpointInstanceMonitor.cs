namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using Particular.HealthMonitoring.Uptime.Api;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;

    class EndpointInstanceMonitor
    {
        public EndpointInstanceId Id { get; }
        DateTime? lastSeen;
        HeartbeatStatus status;

        public bool Monitored { get; private set; }

        public EndpointInstanceMonitor(EndpointInstanceId endpointInstanceId)
        {
            Id = endpointInstanceId;
        }

        public bool TryApply(IHeartbeatEvent @event)
        {
            return TryApply<MonitoringEnabledForEndpoint>(@event, Apply)
                   || TryApply<MonitoringDisabledForEndpoint>(@event, Apply)
                   || TryApply<HeartbeatingEndpointDetected>(@event, Apply)
                   || TryApply<EndpointHeartbeatRestored>(@event, Apply)
                   || TryApply<EndpointFailedToHeartbeat>(@event, Apply);
        }

        static bool TryApply<T>(IHeartbeatEvent @event, Func<T, IDomainEvent> applyFunc)
            where T : class, IHeartbeatEvent
        {
            var typed = @event as T;
            if (typed != null)
            {
                applyFunc(typed);
                return true;
            }
            return false;
        }

        public IHeartbeatEvent EnableMonitoring()
        {
            return Apply(new MonitoringEnabledForEndpoint
            {
                EndpointInstanceId = Id.UniqueId,
                Endpoint = Convert(Id)
            });
        }

        public IHeartbeatEvent DisableMonitoring()
        {
            return Apply(new MonitoringDisabledForEndpoint
            {
                EndpointInstanceId = Id.UniqueId,
                Endpoint = Convert(Id)
            });
        }

        public IHeartbeatEvent StartTrackingEndpoint()
        {
            return Apply(new EndpointDetected
            {
                EndpointInstanceId = Id.UniqueId,
                Endpoint = Convert(Id)
            });
        }

        public IHeartbeatEvent UpdateStatus(HeartbeatStatus newStatus, DateTime? latestTimestamp, DateTime currentTime)
        {
            if (newStatus == status)
            {
                return null;
            }

            if (newStatus == HeartbeatStatus.Alive)
            {
                if (status == HeartbeatStatus.Unknown)
                {
                    // NOTE: If an endpoint starts randomly sending heartbeats we monitor it by default
                    // NOTE: This means we'll start monitoring endpoints sending heartbeats after a restart
                    return Apply(new HeartbeatingEndpointDetected
                    {
                        EndpointInstanceId = Id.UniqueId,
                        Endpoint = Convert(Id),
                        DetectedAt = latestTimestamp ?? DateTime.UtcNow
                    });
                }

                if (status == HeartbeatStatus.Dead && Monitored)
                {
                    return Apply(new EndpointHeartbeatRestored
                    {
                        EndpointInstanceId = Id.UniqueId,
                        Endpoint = Convert(Id),
                        RestoredAt = latestTimestamp ?? DateTime.UtcNow
                    });
                }
            }
            else if (newStatus == HeartbeatStatus.Dead && Monitored)
            {
                return Apply(new EndpointFailedToHeartbeat
                {
                    EndpointInstanceId = Id.UniqueId,
                    Endpoint = Convert(Id),
                    DetectedAt = currentTime,
                    LastReceivedAt = latestTimestamp ?? DateTime.MinValue
                });
            }

            return null;
        }

        IHeartbeatEvent Apply(EndpointDetected @event)
        {
            return @event;
        }

        IHeartbeatEvent Apply(HeartbeatingEndpointDetected @event)
        {
            Monitored = true;
            status = HeartbeatStatus.Alive;
            lastSeen = @event.DetectedAt;
            return @event;
        }

        IHeartbeatEvent Apply(EndpointHeartbeatRestored @event)
        {
            Monitored = true;
            status = HeartbeatStatus.Alive;
            lastSeen = @event.RestoredAt;
            return @event;
        }

        IHeartbeatEvent Apply(EndpointFailedToHeartbeat @event)
        {
            Monitored = true;
            status = HeartbeatStatus.Dead;
            lastSeen = @event.LastReceivedAt;
            return @event;
        }

        IHeartbeatEvent Apply(MonitoringEnabledForEndpoint @event)
        {
            Monitored = true;
            return @event;
        }

        IHeartbeatEvent Apply(MonitoringDisabledForEndpoint @event)
        {
            Monitored = false;
            return @event;
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

        EndpointDetails Convert(EndpointInstanceId endpointInstanceId)
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
    }
}