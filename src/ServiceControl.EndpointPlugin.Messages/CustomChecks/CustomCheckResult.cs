namespace ServiceControl.EndpointPlugin.Messages.CustomChecks
{
    public class CustomCheckResult
    {
        public bool HasFailed { get; set; }
        public string FailureReason { get; set; }

        public static CustomCheckResult Ok
        {
            get
            {
                return new CustomCheckResult();
            }

        }

        public static CustomCheckResult Failed(string reason)
        {
            return new CustomCheckResult
            {
                HasFailed = true,
                FailureReason = reason
            };
        }
    }
}
