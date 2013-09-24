namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messages.CustomChecks;
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
        CancellationTokenSource tokenSource = new CancellationTokenSource();
             
        public void Start()
        {
            var cancellationToken = tokenSource.Token;
            Builder.BuildAll<IPeriodicCheck>().ToList()
                .ForEach(p => Task.Factory.StartNew(() =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var result = p.PerformCheck();
                        ReportToBackend(result, p.PeriodicCheckId, p.Category, TimeSpan.FromTicks(p.Interval.Ticks * 4));

                        //if (result.HasFailed)
                        //    ReportCustomCheckResult
                        Thread.Sleep(p.Interval);
                    }
                }, cancellationToken));
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
    }
}
