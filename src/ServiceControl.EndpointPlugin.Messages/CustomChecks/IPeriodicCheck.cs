namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
    using System;

    // needed for DI
    public interface IPeriodicCheck
    {
        string Category { get; }
        string PeriodicCheckId { get; }
        CheckResult PerformCheck();
        TimeSpan Interval { get; }
    }
}
