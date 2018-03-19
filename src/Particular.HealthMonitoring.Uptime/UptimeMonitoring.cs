namespace Particular.HealthMonitoring.Uptime
{
    using System;
    using System.Threading;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Monitoring;

    public class UptimeMonitoring
    {
        public EndpointInstanceMonitoring Monitoring { get; }

        ITimeKeeper timeKeeper;
        TimeSpan gracePeriod;
        Timer timer;

        public UptimeMonitoring(TimeSpan gracePeriod, IDomainEvents domainEvents, ITimeKeeper timeKeeper)
        {
            this.timeKeeper = timeKeeper;
            this.gracePeriod = gracePeriod;

            Monitoring = new EndpointInstanceMonitoring(domainEvents);
        }

        public void Start()
        {
            timer = timeKeeper.New(CheckEndpoints, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        void CheckEndpoints()
        {
            try
            {
                var inactivityThreshold = DateTime.UtcNow - gracePeriod;
                //log.Debug($"Monitoring Endpoint Instances. Inactivity Threshold = {inactivityThreshold}");
                Monitoring.CheckEndpoints(inactivityThreshold);
            }
            catch (Exception exception)
            {
                //log.Error("Exception occurred when monitoring endpoint instances", exception);
            }
        }

        public void Stop()
        {
            timeKeeper.Release(timer);
        }

        //private static ILog log = LogManager.GetLogger<UptimeMonitoring>();
    }
}