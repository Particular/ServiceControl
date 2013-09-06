namespace ServiceControl.HeartbeatMonitoring
{
    using NServiceBus;

    public class HeartbeatMonitorStarter : IWantToRunWhenBusStartsAndStops
    {
        readonly HeartbeatMonitor monitor;

        public HeartbeatMonitorStarter(HeartbeatMonitor monitor)
        {
            this.monitor = monitor;
        }

        public void Start()
        {
            monitor.Start();
        }

        public void Stop()
        {
            monitor.Stop();
        }
    }
}