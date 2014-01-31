namespace ServiceControl.Plugin.CustomChecks.Messages
{
    using System;

    public class ReportCustomCheckResult
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public CheckResult Result { get; set; }
        public DateTime ReportedAt { get; set; }
    }

    public class CheckResult
    {
        public bool HasFailed { get; set; }
        public string FailureReason { get; set; }

        public static CheckResult Pass
        {
            get
            {
                return new CheckResult();
            }
        }

        public static CheckResult Failed(string reason)
        {
            return new CheckResult
            {
                HasFailed = true,
                FailureReason = reason
            };
        }
    }
}
