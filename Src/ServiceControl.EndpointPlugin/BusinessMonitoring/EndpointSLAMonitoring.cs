namespace ServiceControl.EndpointPlugin.BusinessMonitoring
{
    using Infrastructure.PerformanceCounters;
    using NServiceBus;
    using NServiceBus.Features;

    public class EndpointSLAMonitoring : Feature, IWantToRunWhenBusStartsAndStops
    {
        public PerformanceCounterCapturer PerformanceCounterCapturer { get; set; }

        public void Start()
        {
            PerformanceCounterCapturer.EnableCapturing("NServiceBus", "Critical Time", Configure.EndpointName, "CriticalTime");
        }

        public void Stop()
        {
        }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

    }
}