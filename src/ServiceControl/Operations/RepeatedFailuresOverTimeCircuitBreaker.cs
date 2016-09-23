namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;

    /// <summary>
    ///     A circuit breaker that triggers after a given time
    /// </summary>
    public class RepeatedFailuresOverTimeCircuitBreaker : IDisposable
    {
        private static readonly TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        private static readonly ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();

        private readonly TimeSpan delayAfterFailure;
        private readonly string name;
        private readonly Action<Exception> triggerAction;
        private bool disposed;
        private int disposeSignaled;
        private long failureCount;
        private Exception lastException;
        private Timer timer;
        private readonly TimeSpan timeToWaitBeforeTriggering;

        /// <summary>
        ///     Ctor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="timeToWaitBeforeTriggering"></param>
        /// <param name="triggerAction"></param>
        public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering,
            Action<Exception> triggerAction)
            : this(name, timeToWaitBeforeTriggering, triggerAction, TimeSpan.FromSeconds(1))
        {
        }

        /// <summary>
        ///     Ctor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="timeToWaitBeforeTriggering"></param>
        /// <param name="triggerAction"></param>
        /// <param name="delayAfterFailure"></param>
        public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering,
            Action<Exception> triggerAction, TimeSpan delayAfterFailure)
        {
            this.name = name;
            this.delayAfterFailure = delayAfterFailure;
            this.triggerAction = triggerAction;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;

            timer = new Timer(CircuitBreakerTriggered);
        }

        /// <summary>
        ///     Disposes the CB
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposeSignaled, 1) != 0)
            {
                return;
            }
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
            disposed = true;
        }

        /// <summary>
        ///     Tell the CB that it should disarm
        /// </summary>
        public bool Success()
        {
            ThrowIfDisposed();

            var oldValue = Interlocked.Exchange(ref failureCount, 0);

            if (oldValue == 0)
            {
                return false;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.Info($"The circuit breaker for {name} is now disarmed.");

            return true;
        }

        /// <summary>
        ///     Tells the CB to arm
        /// </summary>
        /// <param name="exception"></param>
        public Task Failure(Exception exception)
        {
            ThrowIfDisposed();

            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.Info($"The circuit breaker for {name} is now in the armed state");
            }

            return Task.Delay(delayAfterFailure);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("RepeatedFailuresOverTimeCircuitBreaker");
            }
        }

        private void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.Read(ref failureCount) > 0)
            {
                Logger.Warn($"The circuit breaker for {name} will now be triggered");
                triggerAction(lastException);
            }
        }
    }
}