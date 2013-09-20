namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using Messages.Operations.ServiceControlBackend;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;

    /// <summary>
    /// This class will upon startup get the registered PeriodicCheck types
    /// and will invoke each one's PerformCheck at the desired interval.
    /// </summary>
    class PeriodicCheckMonitor : IWantToRunWhenBusStartsAndStops
    {
        public IServiceControlBackend ServiceControlBackend { get; set; }
        public IBuilder Builder { get; set; }

        public void Start()
        {
            //TODO - Complete this.
            // Get the registered instances of ICustomCheck types.
        }

        public void Stop()
        {
            
        }
    }
}
