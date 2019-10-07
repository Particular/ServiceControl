namespace ServiceControl.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    enum TimerJobExecutionResult
    {
        ScheduleNextExecution,
        ExecuteImmediately,
        DoNotContinueExecuting
    }

    class AsyncTimer
    {
        public AsyncTimer(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback)
        {
            Start(callback, due, interval, errorCallback);
        }

        void Start(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback)
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

        public Task Stop()
        {
            if (tokenSource == null)
            {
                return Task.CompletedTask;
            }

            tokenSource.Cancel();
            tokenSource.Dispose();

            return task ?? Task.CompletedTask;
        }

        Task task;
        CancellationTokenSource tokenSource;
    }
}