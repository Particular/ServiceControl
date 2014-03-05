namespace ServiceControl.Plugin.CustomChecks.Internal
{
    using System;
    using System.Threading;
    using EndpointPlugin.Operations.ServiceControlBackend;
    using Messages;
    using NServiceBus.Logging;

    public class TimerBasedPeriodicCheck : IDisposable
    {
        static TimerBasedPeriodicCheck()
        {
            var hostInfo =  HostInformationRetriever.RetrieveHostInfo();

            hostId = hostInfo.HostId;
        }

        public TimerBasedPeriodicCheck(IPeriodicCheck periodicCheck, ServiceControlBackend serviceControlBackend)
        {
            this.periodicCheck = periodicCheck;
            this.serviceControlBackend = serviceControlBackend;

            timer = new Timer(Run, null, TimeSpan.Zero, periodicCheck.Interval);
        }

        public void Dispose()
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                timer.Dispose(waitHandle);

                waitHandle.WaitOne();
            }
        }

        void ReportToBackend(CheckResult result, string customCheckId, string category, TimeSpan ttr)
        {
            serviceControlBackend.Send(new ReportCustomCheckResult
            {
                HostId = hostId,
                CustomCheckId = customCheckId,
                Category = category,
                Result = result,
                ReportedAt = DateTime.UtcNow
            }, ttr);
        }

        void Run(object state)
        {
            CheckResult result;
            try
            {
                result = periodicCheck.PerformCheck();
            }
            catch (Exception ex)
            {
                var reason = String.Format("'{0}' implementation failed to run.", periodicCheck.GetType());
                result = CheckResult.Failed(reason);
                Logger.Error(reason, ex);
            }

            try
            {
                ReportToBackend(result, periodicCheck.Id, periodicCheck.Category,
                    TimeSpan.FromTicks(periodicCheck.Interval.Ticks*4));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to report periodic check to ServiceControl.", ex);
            }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(TimerBasedPeriodicCheck));
        readonly IPeriodicCheck periodicCheck;
        readonly ServiceControlBackend serviceControlBackend;
        readonly Timer timer;
        static readonly Guid hostId;

    }
}