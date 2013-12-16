namespace ServiceControl.Plugin.CustomChecks.Internal
{
    using System;
    using Messages;

    // needed for DI
    public interface IPeriodicCheck
    {
        string Category { get; }
        string Id { get; }
        CheckResult PerformCheck();
        TimeSpan Interval { get; }
    }
}
