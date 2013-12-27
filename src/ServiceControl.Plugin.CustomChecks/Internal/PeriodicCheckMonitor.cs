namespace ServiceControl.Plugin.CustomChecks.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EndpointPlugin.Operations.ServiceControlBackend;
    using Messages;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;

    /// <summary>
    ///     This class will upon startup get the registered PeriodicCheck types
    ///     and will invoke each one's PerformCheck at the desired interval.
    /// </summary>
    internal class PeriodicCheckMonitor : IWantToRunWhenBusStartsAndStops
    {
        public ServiceControlBackend ServiceControlBackend { get; set; }
        public IBuilder Builder { get; set; }

        public void Start()
        {
            var cancellationToken = tokenSource.Token;
            periodicChecks = Builder.BuildAll<IPeriodicCheck>().ToList();

            periodicChecks.ForEach(p => Task.Factory.StartNew(() =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        CheckResult result;
                        try
                        {
                            result = p.PerformCheck();
                        }
                        catch (Exception ex)
                        {
                            var reason = String.Format("'{0}' implementation failed to run.", p.GetType());
                            result = CheckResult.Failed(reason);
                            Logger.Error(reason, ex);
                        }

                        try
                        {
                            ReportToBackend(result, p.Id, p.Category, TimeSpan.FromTicks(p.Interval.Ticks*4));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to report periodic check to ServiceControl.", ex);
                        }

                        Thread.Sleep(p.Interval);
                    }
                }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default));

            customChecks = Builder.BuildAll<ICustomCheck>().ToList();
        }

        public void Stop()
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
            }
        }

        void ReportToBackend(CheckResult result, string customCheckId, string category, TimeSpan ttr)
        {
            ServiceControlBackend.Send(new ReportCustomCheckResult
            {
                CustomCheckId = customCheckId,
                Category = category,
                Result = result,
                ReportedAt = DateTime.UtcNow
            }, ttr);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(PeriodicCheckMonitor));
        readonly CancellationTokenSource tokenSource = new CancellationTokenSource();
        List<ICustomCheck> customChecks;
        List<IPeriodicCheck> periodicChecks;
    }
}