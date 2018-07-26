namespace ServiceControl.LoadTests.Reporter
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Messages;
    using NServiceBus;
    using NServiceBus.Features;

    class ReportProcessingStatistics : FeatureStartupTask
    {
        Statistics statistics;
        string auditQueueAddress;
        string loadGenetorQueue;
        Task reportTask;
        CancellationTokenSource tokenSource;

        public ReportProcessingStatistics(Statistics statistics, string auditQueueAddress, string loadGenetorQueue)
        {
            this.statistics = statistics;
            this.auditQueueAddress = auditQueueAddress;
            this.loadGenetorQueue = loadGenetorQueue;
        }

        protected override Task OnStart(IMessageSession session)
        {
            tokenSource = new CancellationTokenSource();
            reportTask = Task.Run(async () =>
            {
                while (!tokenSource.IsCancellationRequested)
                {
                    await Task.Delay(2000, tokenSource.Token).ConfigureAwait(false);
                    try
                    {
                        var message = new ProcessingReport
                        {
                            Audits = statistics.Audits,
                            AuditQueue = auditQueueAddress,
                            HostId = statistics.HostId
                        };
                        await session.Send(loadGenetorQueue, message).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            });
            return Task.CompletedTask;
        }

        protected override Task OnStop(IMessageSession session)
        {
            tokenSource?.Cancel();
            return reportTask ?? Task.CompletedTask;
        }
    }
}
