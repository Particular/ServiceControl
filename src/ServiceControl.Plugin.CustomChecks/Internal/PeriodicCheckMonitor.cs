namespace ServiceControl.Plugin.CustomChecks.Internal
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointPlugin.Operations.ServiceControlBackend;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transports;

    /// <summary>
    ///     This class will upon startup get the registered PeriodicCheck types
    ///     and will invoke each one's PerformCheck at the desired interval.
    /// </summary>
    class PeriodicCheckMonitor : IWantToRunWhenBusStartsAndStops
    {
        public ISendMessages MessageSender { get; set; }
        public IBuilder Builder { get; set; }

        public void Start()
        {
            var periodicChecks = Builder.BuildAll<IPeriodicCheck>().ToList();
            timerPeriodicChecks = new List<TimerBasedPeriodicCheck>(periodicChecks.Count);

            foreach (var check in periodicChecks)
            {
                timerPeriodicChecks.Add(new TimerBasedPeriodicCheck(check, MessageSender));
            }

            customChecks = Builder.BuildAll<ICustomCheck>().ToList();
        }

        public void Stop()
        {
            Parallel.ForEach(timerPeriodicChecks, t => t.Dispose());
        }

// ReSharper disable NotAccessedField.Local
        List<ICustomCheck> customChecks;
// ReSharper restore NotAccessedField.Local
        List<TimerBasedPeriodicCheck> timerPeriodicChecks;
    }
}