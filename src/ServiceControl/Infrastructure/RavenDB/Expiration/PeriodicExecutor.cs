namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class PeriodicExecutor
    {
        readonly Action action;
        readonly TimeSpan period;
        CancellationTokenSource tokenSource;

        public PeriodicExecutor(Action action, TimeSpan period)
        {
            this.action = action;
            this.period = period;
        }

        public void Start(bool delay)
        {
            if (tokenSource != null)
            {
                throw new InvalidOperationException("Executor has already been started");
            }
            tokenSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                var cancelToken = tokenSource.Token;

                if (delay)
                    await Task.Delay(period, cancelToken);

                while (!cancelToken.IsCancellationRequested)
                {
                    var nextTime = DateTime.Now + period;

                    try
                    {
                        action();
                    }
                    // ReSharper disable once EmptyGeneralCatchClause
                    catch
                    {
                        // We swallow exceptions so the timer keeps going.
                        // Should we log these?
                    }

                    var delayPeriod = nextTime - DateTime.Now;
                    if (delayPeriod > TimeSpan.Zero)
                        await Task.Delay(delayPeriod, cancelToken);
                }
            }, tokenSource.Token);
        }

        public void Stop()
        {
            if (tokenSource == null)
            {
                throw new InvalidOperationException("Executor has not been started");
            }

            tokenSource.Cancel();
        }
    }
}