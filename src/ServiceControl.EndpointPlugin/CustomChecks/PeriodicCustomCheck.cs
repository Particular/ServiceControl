namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;

    public  abstract class PeriodicCustomCheck : CustomCheck
    {
        protected virtual TimeSpan Interval
        {
            get
            {
                return TimeSpan.FromMinutes(1);
            }
        }

        public abstract void PerformCheck();
    }
}