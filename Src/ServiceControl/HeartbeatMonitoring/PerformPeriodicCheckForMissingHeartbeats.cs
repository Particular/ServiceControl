namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using System.Threading;
    using NServiceBus;

    public class PerformPeriodicCheckForMissingHeartbeats:IWantToRunWhenBusStartsAndStops
    {
        public HeartbeatMonitor HeartbeatMonitor { get; set; }

        public void Start()
        {
            timer = new Timer(PerformCheck,null,TimeSpan.Zero,TimeSpan.FromSeconds(1));
        }

        void PerformCheck(object state)
        {
            HeartbeatMonitor.CheckForMissingHeartbeats();
        }

        public void Stop()
        {
            timer.Dispose();
        }

        Timer timer;
    }
}