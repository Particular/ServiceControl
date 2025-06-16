namespace NServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    class RepeatedFailuresOverTimeCircuitBreaker : IDisposable
    {
        public RepeatedFailuresOverTimeCircuitBreaker(
            string name,
            TimeSpan timeToWaitBeforeTriggering,
            Action<Exception> triggerAction,
            TimeSpan delayAfterFailure,
            ILogger<RepeatedFailuresOverTimeCircuitBreaker> logger)
        {
            this.name = name;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;
            this.triggerAction = triggerAction;
            this.delayAfterFailure = delayAfterFailure;
            this.logger = logger;

            timer = new Timer(CircuitBreakerTriggered);
        }

        public void Dispose()
        {
            timer.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Success()
        {
            var oldValue = Interlocked.Exchange(ref failureCount, 0);

            if (oldValue == 0)
            {
                return;
            }

            if (timer.Change(Timeout.Infinite, Timeout.Infinite))
            {
                logger.LogInformation("The circuit breaker for {circuitBreakerName} is now disarmed", name);
            }
            else
            {
                logger.LogError("Attempted to disarm circuit breaker for {circuitBreakerName} but failed", name);
            }
        }

        public Task Failure(Exception exception)
        {
            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                if (timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering))
                {
                    logger.LogWarning("The circuit breaker for {circuitBreakerName} is now in the armed state", name);
                }
                else
                {
                    logger.LogError("Attempted to arm circuit breaker for {circuitBreakerName} but failed", name);
                }
            }

            return Task.Delay(delayAfterFailure);
        }

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.Read(ref failureCount) > 0)
            {
                logger.LogWarning("The circuit breaker for {circuitBreakerName} will now be triggered", name);
                triggerAction(lastException);
            }
        }

        readonly TimeSpan delayAfterFailure;
        long failureCount;
        Exception lastException;

        readonly string name;
        readonly Timer timer;
        readonly TimeSpan timeToWaitBeforeTriggering;
        readonly Action<Exception> triggerAction;

        static readonly TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        readonly ILogger<RepeatedFailuresOverTimeCircuitBreaker> logger;
    }
}