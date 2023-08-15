﻿namespace ServiceControl.Infrastructure.BackgroundTasks
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

            task = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(due, token).ConfigureAwait(false);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var result = await callback(token).ConfigureAwait(false);
                            if (result == TimerJobExecutionResult.DoNotContinueExecuting)
                            {
                                tokenSource.Cancel();
                            }
                            else if (result == TimerJobExecutionResult.ScheduleNextExecution)
                            {
                                await Task.Delay(interval, token).ConfigureAwait(false);
                            }

                            //Otherwise execute immediately
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
            }, CancellationToken.None);
        }

        public async Task Stop()
        {
            if (tokenSource == null)
            {
                return;
            }

            tokenSource.Cancel();
            tokenSource.Dispose();

            if (task != null)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    //NOOP
                }
            }
        }

        Task task;
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