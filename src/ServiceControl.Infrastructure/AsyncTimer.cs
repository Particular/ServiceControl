namespace ServiceControl.Infrastructure.BackgroundTasks;

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

                var consecutiveFailures = 0;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await callback(token).ConfigureAwait(false);

                        consecutiveFailures = 0;
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
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        consecutiveFailures++;
                        var exponentialBackoffDelay = TimeSpan.FromSeconds(int.Max(60, consecutiveFailures * consecutiveFailures));

                        await Task.Delay(exponentialBackoffDelay, token).ConfigureAwait(false);

                        errorCallback(ex);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // no-op
            }
        }, CancellationToken.None);
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (tokenSource == null)
        {
            return;
        }

        await tokenSource.CancelAsync().ConfigureAwait(false);
        tokenSource.Dispose();

        if (task == null)
        {
            return;
        }

        try
        {
            Task.WaitAll([task], cancellationToken);
        }
        catch (OperationCanceledException) when (tokenSource.IsCancellationRequested)
        {
            //NOOP
        }
    }

    readonly Task task;
    readonly CancellationTokenSource tokenSource;
}

public interface IAsyncTimer
{
    TimerJob Schedule(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback);
}

public class AsyncTimer : IAsyncTimer
{
    public TimerJob Schedule(Func<CancellationToken, Task<TimerJobExecutionResult>> callback, TimeSpan due, TimeSpan interval, Action<Exception> errorCallback) => new(callback, due, interval, errorCallback);
}