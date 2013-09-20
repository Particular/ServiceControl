namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using Internal;

    public abstract class PeriodicCheck : IPeriodicCheck
    {
        public abstract CheckResult PerformCheck();

        public virtual System.TimeSpan Interval
        {
            get { return TimeSpan.FromMinutes(1); }
        }

    }
}
