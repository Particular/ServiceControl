namespace ServiceControl.Operations
{
    using System;
    using System.Diagnostics;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    internal class MsmqAuditImporterPerformanceCounters : IDisposable
    {
        public void Dispose()
        {
            if (successRateCounter != null)
            {
                successRateCounter.Dispose();
            }
            if (throughputCounter != null)
            {
                throughputCounter.Dispose();
            }
        }

        public void MessageProcessed()
        {
            successRateCounter.Increment();
        }

        public void MessageDequeued()
        {
            if (!enabled)
            {
                return;
            }

            throughputCounter.Increment();
        }

        public void Initialize()
        {
            if (!InstantiateCounter())
            {
                return;
            }

            enabled = true;
        }

        bool InstantiateCounter()
        {
            return SetupCounter("# of msgs successfully processed / sec", ref successRateCounter)
                   && SetupCounter("# of msgs pulled from the input queue /sec", ref throughputCounter);
        }

        bool SetupCounter(string counterName, ref PerformanceCounter counter)
        {
            try
            {
                counter = new PerformanceCounter(CategoryName, counterName, Settings.AuditQueue.Queue, false);
                //access the counter type to force a exception to be thrown if the counter doesn't exists
                // ReSharper disable once UnusedVariable
                var t = counter.CounterType;
            }
            catch (Exception)
            {
                Logger.InfoFormat(
                    "NServiceBus performance counter for {1} is not set up correctly, no statistics will be emitted for the {0} queue. Execute the Install-NServiceBusPerformanceCounters cmdlet to create the counter.",
                    Settings.AuditQueue.Queue, counterName);
                return false;
            }
            Logger.DebugFormat("'{0}' counter initialized for '{1}'", counterName, Settings.AuditQueue);
            return true;
        }

        const string CategoryName = "NServiceBus";

        static readonly ILog Logger = LogManager.GetLogger(typeof(MsmqAuditImporterPerformanceCounters));
        bool enabled;
        PerformanceCounter successRateCounter, throughputCounter;
    }
}