#nullable enable 

namespace NServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// A circuit breaker that is armed on a failure and disarmed on success. After <see cref="timeToWaitBeforeTriggering"/> in the
/// armed state, the <see cref="triggerAction"/> will fire. The <see cref="armedAction"/> and <see cref="disarmedAction"/> allow
/// changing other state when the circuit breaker is armed or disarmed.
/// </summary>
public sealed class RepeatedFailuresOverTimeCircuitBreaker
{
    /// <summary>
    /// A circuit breaker that is armed on a failure and disarmed on success. After <see cref="timeToWaitBeforeTriggering"/> in the
    /// armed state, the <see cref="triggerAction"/> will fire. The <see cref="armedAction"/> and <see cref="disarmedAction"/> allow
    /// changing other state when the circuit breaker is armed or disarmed.
    /// </summary>
    /// <param name="name">A name that is output in log messages when the circuit breaker changes states.</param>
    /// <param name="timeToWaitBeforeTriggering">The time to wait after the first failure before triggering.</param>
    /// <param name="triggerAction">The action to take when the circuit breaker is triggered.</param>
    /// <param name="armedAction">The action to execute on the first failure.
    /// <b>Warning:</b> This action is also invoked from within a lock. Any long-running, blocking, or I/O-bound code should be avoided 
    /// within this action, as it can prevent other threads from proceeding, potentially leading to contention or performance bottlenecks.
    /// </param>
    /// <param name="disarmedAction">The action to execute when a success disarms the circuit breaker.
    /// <b>Warning:</b> This action is also invoked from within a lock. Any long-running, blocking, or I/O-bound code should be avoided 
    /// within this action, as it can prevent other threads from proceeding, potentially leading to contention or performance bottlenecks.
    /// </param>
    /// <param name="timeToWaitWhenTriggered">How long to delay on each failure when in the Triggered state. Defaults to 10 seconds.</param>
    /// <param name="timeToWaitWhenArmed">How long to delay on each failure when in the Armed state. Defaults to 1 second.</param>
    /// <remarks>
    /// The <see cref="armedAction"/> and <see cref="disarmedAction"/> are invoked from within a lock to ensure that arming and disarming
    /// actions are serialized and do not execute concurrently. As a result, care must be taken to ensure that these actions do not 
    /// introduce delays or deadlocks by performing lengthy operations or synchronously waiting on external resources. 
    /// 
    /// <b>Best practice:</b> If the logic inside these actions involves blocking or long-running tasks, consider offloading 
    /// the work to a background task or thread that doesn't hold the lock.
    /// </remarks>
    public RepeatedFailuresOverTimeCircuitBreaker(
        string name,
        TimeSpan timeToWaitBeforeTriggering,
        Action<Exception> triggerAction,
        ILogger logger,
        Action? armedAction = null,
        Action? disarmedAction = null,
        TimeSpan? timeToWaitWhenTriggered = default,
        TimeSpan? timeToWaitWhenArmed = default)
    {
        this.name = name;
        this.triggerAction = triggerAction;
        this.logger = logger;
        this.armedAction = armedAction ?? (static () => { });
        this.disarmedAction = disarmedAction ?? (static () => { });
        this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;
        this.timeToWaitWhenTriggered = timeToWaitWhenTriggered ?? TimeSpan.FromSeconds(10);
        this.timeToWaitWhenArmed = timeToWaitWhenArmed ?? TimeSpan.FromSeconds(1);

        timer = new Timer(CircuitBreakerTriggered);
    }

    /// <summary>
    /// Log a success, disarming the circuit breaker if it was previously armed.
    /// </summary>
    public void Success()
    {
        // Check the status of the circuit breaker, exiting early outside the lock if already disarmed
        if (Volatile.Read(ref circuitBreakerState) == Disarmed)
        {
            return;
        }

        lock (stateLock)
        {
            // Recheck state after obtaining the lock
            if (circuitBreakerState == Disarmed)
            {
                return;
            }

            circuitBreakerState = Disarmed;

            _ = timer.Change(Timeout.Infinite, Timeout.Infinite);
            logger.LogInformation("The circuit breaker for '{BreakerName}' is now disarmed.", name);
            try
            {
                disarmedAction();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The circuit breaker for '{BreakerName}' was unable to execute the disarm action.", name);
                throw;
            }
        }
    }

    /// <summary>
    /// Log a failure, arming the circuit breaker if it was previously disarmed.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public Task Failure(Exception exception, CancellationToken cancellationToken = default)
    {
        // Atomically store the exception that caused the circuit breaker to trip
        _ = Interlocked.Exchange(ref lastException, exception);

        var previousState = Volatile.Read(ref circuitBreakerState);
        if (previousState is Armed or Triggered)
        {
            return Delay();
        }

        lock (stateLock)
        {
            // Recheck state after obtaining the lock
            previousState = circuitBreakerState;
            if (previousState is Armed or Triggered)
            {
                return Delay();
            }

            circuitBreakerState = Armed;

            try
            {
                // Executing the action first before starting the timer to ensure that the action is executed before the timer fires
                // and the time of the action is not included in the time to wait before triggering.
                armedAction();
            }
            catch (Exception ex)
            {
                logger.LogError(new AggregateException(ex, exception), "The circuit breaker for '{BreakerName}' was unable to execute the arm action.", name);
                throw;
            }

            _ = timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
            logger.LogWarning("The circuit breaker for '{BreakerName}' is now in the armed state due to '{BreakerCause}' and might trigger in '{BreakerTriggerTime}' when not disarmed.", name, exception, timeToWaitBeforeTriggering);
        }

        return Delay();

        Task Delay()
        {
            var timeToWait = previousState == Triggered ? timeToWaitWhenTriggered : timeToWaitWhenArmed;
            logger.LogDebug("The circuit breaker for '{BreakerName}' is delaying the operation by '{BreakerTriggerTime}'.", name, timeToWait);
            return Task.Delay(timeToWait, cancellationToken);
        }
    }

    /// <summary>
    /// Disposes the resources associated with the circuit breaker.
    /// </summary>
    public void Dispose() => timer.Dispose();

    void CircuitBreakerTriggered(object? state)
    {
        var previousState = Volatile.Read(ref circuitBreakerState);
        if (previousState == Disarmed)
        {
            return;
        }

        lock (stateLock)
        {
            // Recheck state after obtaining the lock
            if (circuitBreakerState == Disarmed)
            {
                return;
            }

            circuitBreakerState = Triggered;
            logger.LogWarning("The circuit breaker for '{BreakerName}' will now be triggered with exception '{BreakerCause}'.", name, lastException);

            try
            {
                triggerAction(lastException!);
            }
            catch (Exception ex)
            {
                logger.LogCritical(new AggregateException(ex, lastException!), "The circuit breaker for '{BreakerName}' was unable to execute the trigger action.", name);
            }
        }
    }

    int circuitBreakerState = Disarmed;
    Exception? lastException;

    readonly string name;
    readonly Timer timer;
    readonly TimeSpan timeToWaitBeforeTriggering;
    readonly Action<Exception> triggerAction;
    readonly Action armedAction;
    readonly Action disarmedAction;
    readonly TimeSpan timeToWaitWhenTriggered;
    readonly TimeSpan timeToWaitWhenArmed;
    readonly object stateLock = new();

    const int Disarmed = 0;
    const int Armed = 1;
    const int Triggered = 2;

    static readonly TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
    readonly ILogger logger;
}