namespace ServiceControl.CustomChecks.Internal
{
    using System.Threading.Tasks;

    /// <summary>
    /// The result of a check.
    /// </summary>
    public class CheckResult
    {
        /// <summary>
        /// <code>true</code> if it failed.
        /// </summary>
        public bool HasFailed { get; set; }

        /// <summary>
        /// The reason for the failure.
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Passes a check.
        /// </summary>
        public static CheckResult Pass = new CheckResult();

        /// <summary>
        /// Fails a check.
        /// </summary>
        /// <param name="reason">Reason for failure.</param>
        /// <returns>The result.</returns>
        public static CheckResult Failed(string reason)
        {
            return new CheckResult
            {
                HasFailed = true,
                FailureReason = reason
            };
        }

        /// <summary>
        /// Converts a check result.
        /// </summary>
        /// <param name="result">The converted result.</param>
        public static implicit operator Task<CheckResult>(CheckResult result)
        {
            return Task.FromResult(result);
        }
    }
}