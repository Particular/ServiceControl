namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
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
