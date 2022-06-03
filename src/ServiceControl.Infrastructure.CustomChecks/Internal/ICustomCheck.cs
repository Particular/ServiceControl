namespace ServiceControl.CustomChecks.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to implement a custom check.
    /// </summary>
    public interface ICustomCheck
    {
        /// <summary>
        /// Category for the check.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Check Id.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Periodic execution interval.
        /// </summary>
        TimeSpan? Interval { get; }

        /// <summary>
        /// Performs the check.
        /// </summary>
        /// <returns>The result of the check.</returns>
        Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default);
    }
}