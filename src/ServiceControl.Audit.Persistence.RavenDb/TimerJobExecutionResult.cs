namespace ServiceControl.Audit.Infrastructure
{
    public enum TimerJobExecutionResult
    {
        ScheduleNextExecution,
        ExecuteImmediately,
        DoNotContinueExecuting
    }
}