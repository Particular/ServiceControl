namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
    using System;

    // needed for DI
    public interface IPeriodicCheck
    {
        string Category { get; }
        string Id { get; }
        CheckResult PerformCheck();
        TimeSpan Interval { get; }
    }
}
