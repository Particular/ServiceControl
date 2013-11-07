namespace ServiceControl.EndpointPlugin.BusinessMonitoring
{
    using NServiceBus;
    using NServiceBus.Features;
    using Operations.PerformanceCounters;

    class EndpointSLAMonitoring : Feature, IWantToRunWhenBusStartsAndStops
    {
        public PerformanceCounterCapturer PerformanceCounterCapturer { get; set; }

        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public void Start()
        {
            PerformanceCounterCapturer.EnableCapturing("NServiceBus", "Critical Time", Configure.EndpointName,
                "CriticalTime");
        }

        public void Stop()
        {
        }
    }
}