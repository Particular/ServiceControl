namespace ServiceControl.EndpointPlugin.CustomChecks.Internal
{
    using System;

    // needed for DI
    public interface IPeriodicCheck
    {
        CheckResult PerformCheck();
        TimeSpan Interval { get; }
    }

    public class CheckResult
    {
        public bool HasFailed { get; set; }
        public string FailureReason { get; set; }
    }
}
