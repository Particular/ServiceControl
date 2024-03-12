#nullable enable

namespace ServiceControl.Infrastructure.BackgroundTasks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public enum TimerJobExecutionResult
    {
        ScheduleNextExecution,
        ExecuteImmediately,
        DoNotContinueExecuting
    }

    public class TimerJob
    {
        public TimerJob(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback)
        {
            tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;

            timerBody = TimerBody(callback, due, interval, errorCallback, token);
        }

        async Task TimerBody(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(due, cancellationToken).ConfigureAwait(false);

                using var timer = new PeriodicTimer(interval);
                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        TimerJobExecutionResult result;
                        do
                        {
                            result = await callback(cancellationToken).ConfigureAwait(false);

                            if (result == TimerJobExecutionResult.DoNotContinueExecuting)
                            {
                                return;
                            }
                        } while (result == TimerJobExecutionResult.ExecuteImmediately);
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception ex)
                    {
                        errorCallback(ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // no-op
            }
        }

        public async Task Stop()
        {
            await tokenSource.CancelAsync().ConfigureAwait(false);
            tokenSource.Dispose();

            await timerBody.ConfigureAwait(false);
        }

        Task timerBody;
        CancellationTokenSource tokenSource;
    }

    public interface IAsyncTimer
    {
        TimerJob Schedule(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback);
    }

    public class AsyncTimer : IAsyncTimer
    {
        public TimerJob Schedule(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback) => new TimerJob(callback, due, interval, errorCallback);
    }
}