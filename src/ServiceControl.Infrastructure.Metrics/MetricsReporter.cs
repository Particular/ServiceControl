namespace ServiceControl.Infrastructure.Metrics
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class MetricsReporter
    {
        readonly Metrics metrics;
        readonly Action<string> printLine;
        readonly TimeSpan interval;
        CancellationTokenSource tokenSource;
        Task task;

        public MetricsReporter(Metrics metrics, Action<string> printLine, TimeSpan interval)
        {
            this.metrics = metrics;
            this.printLine = printLine;
            this.interval = interval;
        }

        public void Start()
        {
            tokenSource = new CancellationTokenSource();
            task = Task.Run(async () =>
            {
                try
                {
                    while (!tokenSource.IsCancellationRequested)
                    {
                        Print();
                        await Task.Delay(interval, tokenSource.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    //no-op
                }
            }, CancellationToken.None);
        }

        void Print()
        {
            if (!metrics.Enabled)
            {
                return;
            }

            var values = metrics.GetMeterValues();
            foreach (var metricSet in values)
            {
                printLine(
                    $"{metricSet.Name,-40};{metricSet.Current:F};{metricSet.Average15:F};{metricSet.Average60:F};{metricSet.Average300:F}");
            }
        }

        public Task Stop()
        {
            if (tokenSource == null)
            {
                return Task.CompletedTask;
            }
            tokenSource.Cancel();
            return task;
        }
    }
}