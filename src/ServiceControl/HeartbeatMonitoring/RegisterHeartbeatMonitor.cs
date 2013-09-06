namespace ServiceControl.HeartbeatMonitoring
{
    using NServiceBus;

    public class RegisterHeartbeatMonitor : INeedInitialization
    {
        public void Init()
        {
            Configure.Component<HeartbeatMonitor>(DependencyLifecycle.SingleInstance);
        }
    }
}