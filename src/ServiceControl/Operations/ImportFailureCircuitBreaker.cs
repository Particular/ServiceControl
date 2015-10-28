namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using NServiceBus;

    public class ImportFailureCircuitBreaker : IDisposable
    {
        readonly CriticalError criticalError;
        Timer timer;
        long failureCount;

        public ImportFailureCircuitBreaker(CriticalError criticalError)
        {
            this.criticalError = criticalError;
            timer = new Timer(_ => FlushHistory(), null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(20));
        }

        void FlushHistory()
        {
            Interlocked.Exchange(ref failureCount, 0);
        }

        public void Increment(Exception lastException)
        {
            var result = Interlocked.Increment(ref failureCount);
            if (result > 50)
            {
                criticalError.Raise("Failed to import too many times", lastException);
            }
        }

        public void Dispose()
        {
            if (timer != null)
            {
                timer.Dispose();
            }
        }
    }
}