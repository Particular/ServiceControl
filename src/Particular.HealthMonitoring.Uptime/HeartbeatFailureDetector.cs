namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Particular.HealthMonitoring.Uptime.Api;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;

    class HeartbeatFailureDetector : IStartable
    {
        EndpointInstanceMonitoring monitoring;
        TimeSpan gracePeriod;
        Timer timer;

        static readonly ILog log = LogManager.GetLogger<HeartbeatFailureDetector>();
        private IPersistEndpointUptimeInformation persister;
        private IDomainEvents domainEvents;

        public HeartbeatFailureDetector(EndpointInstanceMonitoring monitoring, IDomainEvents domainEvents, IPersistEndpointUptimeInformation persister)
        {
            this.domainEvents = domainEvents;
            this.persister = persister;
            gracePeriod = GetHeartbeatGracePeriod();
            this.monitoring = monitoring;
        }

        static TimeSpan GetHeartbeatGracePeriod()
        {
            try
            {
                return TimeSpan.Parse(SettingsReader<string>.Read("HeartbeatGracePeriod", "00:00:40"));
            }
            catch (Exception ex)
            {
                log.Error($"HeartbeatGracePeriod settings invalid - {ex}. Defaulting HeartbeatGracePeriod to '00:00:40'");
                return TimeSpan.FromSeconds(40);
            }
        }

        public Task Start(ITimeKeeper timeKeeper)
        {
            timer = timeKeeper.New(CheckEndpoints, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            return Task.FromResult(0);
        }

        void CheckEndpoints()
        {
            try
            {
                var now = DateTime.UtcNow;
                var inactivityThreshold = now - gracePeriod;
                log.Debug($"monitoring Endpoint Instances. Inactivity Threshold = {inactivityThreshold}");

                var events = monitoring.CheckEndpoints(inactivityThreshold, now);
                var enumerable = events as IDomainEvent[] ?? events.ToArray();
                foreach (var domainEvent in enumerable)
                {
                    domainEvents.Raise(domainEvent);
                }

                persister.Store(enumerable.OfType<IHeartbeatEvent>());
            }
            catch (Exception exception)
            {
                log.Error("Exception occurred when monitoring endpoint instances", exception);
            }
        }

        public Task Stop(ITimeKeeper timeKeeper)
        {
            timeKeeper.Release(timer);
            return Task.FromResult(0);
        }
    }
}