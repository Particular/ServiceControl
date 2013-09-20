namespace ServiceControl.EndpointPlugin.Operations.Heartbeats
{
    using System;
    using CustomChecks.Internal;
    using ServiceControl.EndpointPlugin.CustomChecks;

    // Here's a thought -- Hearbeats can also be a periodic check. The custom check framework can apply to  
    public class HeartbeatCheck : PeriodicCheck
    {
        public override System.TimeSpan Interval
        {
            get
            {
                return TimeSpan.FromSeconds(20);
            }
        }

        public override CheckResult PerformCheck()
        {
           return new CheckResult() { HasFailed = false };
        }
    }
}
