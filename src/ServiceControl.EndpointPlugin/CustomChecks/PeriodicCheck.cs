namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using System.Linq;
    using Messages.CustomChecks;
    
    public abstract class PeriodicCheck : IPeriodicCheck
    {
        public abstract CheckResult PerformCheck();
        
        public virtual System.TimeSpan Interval
        {
            get { return TimeSpan.FromMinutes(1); }
        }

        public virtual string Category
        {
            get
            {
                return GetType().Namespace.Split('.').Last().Replace("Checks", "");
            }
        }

        public string PeriodicCheckId
        {
            get
            {
                return GetType().FullName;
            }
        }
    }
}
