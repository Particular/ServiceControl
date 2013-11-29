namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.ObjectBuilder;
    using Operations.ServiceControlBackend;
    using Plugin.CustomChecks;
    using Plugin.CustomChecks.Messages;

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
            Builder.BuildAll<IPeriodicCheck>().ToList()
                .ForEach(p => Task.Factory.StartNew(() =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var result = p.PerformCheck();
                        ReportToBackend(result, p.Id, p.Category, TimeSpan.FromTicks(p.Interval.Ticks*4));

                        Thread.Sleep(p.Interval);
                    }
                }, cancellationToken));

            Builder.BuildAll<ICustomCheck>();
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

        readonly CancellationTokenSource tokenSource = new CancellationTokenSource();
    }
}