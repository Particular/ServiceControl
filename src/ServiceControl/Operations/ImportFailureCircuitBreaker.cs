namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;

    class ImportFailureCircuitBreaker : IDisposable
    {

        public ImportFailureCircuitBreaker(Func<string, Exception, Task> onCriticalError)
        {
            this.onCriticalError = onCriticalError;
            timer = new Timer(_ => FlushHistory(), null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(20));
        }

        public void Dispose()
        {
            timer?.Dispose();
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
                Task.Run(() => onCriticalError("Failed to import too many times", lastException)).Ignore();
            }
        }

        Func<string, Exception, Task> onCriticalError;
        Timer timer;
        long failureCount;
    }
}