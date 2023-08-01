namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class FailureRateCircuitBreaker : IDisposable
    {
        public FailureRateCircuitBreaker(string name, int maximumFailuresPerSecond, Action<Exception> triggerAction)
        {
            this.name = name;
            this.triggerAction = triggerAction;
            maximumFailuresPerThirtySeconds = maximumFailuresPerSecond * 30;
            timer = new Timer(_ => FlushHistory(), null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(30));
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        void FlushHistory()
        {
            Interlocked.Exchange(ref failureCount, 0);
            Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
        }

        public void Failure(Exception lastException)
        {
            var result = Interlocked.Increment(ref failureCount);
            if (result > maximumFailuresPerThirtySeconds)
            {
                _ = Task.Run(() =>
                {
                    Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);
                    triggerAction(lastException);
                });
            }
            else if (result == 1)
            {
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }
        }

        static readonly ILog Logger = LogManager.GetLogger<FailureRateCircuitBreaker>();
        readonly string name;
        readonly Action<Exception> triggerAction;
        readonly int maximumFailuresPerThirtySeconds;
        readonly Timer timer;
        long failureCount;
    }
}