namespace ServiceControl.CustomChecks.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstract class to define a custom check implementation.
    /// </summary>
    public abstract class CustomCheck : ICustomCheck
    {
        /// <summary>
        /// Constructor to initialize a custom check.
        /// </summary>
        /// <param name="id">Id to assign.</param>
        /// <param name="category">Category for the check.</param>
        /// <param name="repeatAfter">Periodic execution interval.</param>
        protected CustomCheck(string id, string category, TimeSpan? repeatAfter = null)
        {
            Category = category;
            Id = id;
            Interval = repeatAfter;
        }

        /// <summary>
        /// Category for the check.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Check Id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Periodic execution interval.
        /// </summary>
        public TimeSpan? Interval { get; }

        /// <summary>
        /// Performs the check.
        /// </summary>
        /// <returns>The result of the check.</returns>
        public abstract Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default);
    }
}