namespace ServiceControl.EndpointPlugin.CustomChecks
{
    using System;
    using Messages.CustomChecks;

    public abstract class PeriodicCheck : IPeriodicCheck
    {
        protected PeriodicCheck(string id, string category, TimeSpan interval)
        {
            this.category = category;
            this.id = id;
            this.interval = interval;
        }

        public abstract CheckResult PerformCheck();

        public TimeSpan Interval
        {
            get { return interval; }
        }

        public string Category
        {
            get { return category; }
        }

        public string Id
        {
            get { return id; }
        }

        readonly string id;
        readonly string category;
        readonly TimeSpan interval;
    }
}