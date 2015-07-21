namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class PeriodicExecutor
    {
        Action<PeriodicExecutor> action;
        Action<Exception> onError;
        TimeSpan period;
        CancellationTokenSource tokenSource;

        public PeriodicExecutor(Action<PeriodicExecutor> action, TimeSpan period, Action<Exception> onError = null)
        {
            this.action = action;
            this.period = period;
            this.onError = onError ?? (e => { });
        }

        public bool IsCancellationRequested
        {
            get
            {
                if (tokenSource == null)
                {
                    return true;
                }
                return tokenSource.IsCancellationRequested;
            }
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
                        action(this);
                    }
                    catch (Exception ex)
                    {
                        onError(ex);
                    }

                    var delayPeriod = nextTime - DateTime.Now;
                    if (delayPeriod > TimeSpan.Zero)
                    {
                        await Task.Delay(delayPeriod, cancelToken);
                    }
                }
            }, tokenSource.Token);
        }

        public void Stop()
        {
            if (tokenSource != null)
            {
                tokenSource.Cancel();
            }
        }
    }
}