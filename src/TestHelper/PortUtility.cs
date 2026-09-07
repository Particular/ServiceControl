namespace TestHelper
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net.NetworkInformation;

    public static class PortUtility
    {
        /// <summary>
        /// The 0-based index that <c>Particular/run-tests-action</c> assigns to each spawned
        /// <c>dotnet test</c> process immediately before spawning it, so concurrent runs can derive
        /// distinct per-run resources from it. The value is unique across all runs in the invocation;
        /// in sequential mode (<c>max-parallel == 1</c>) it is always <c>0</c>. Defaults to <c>0</c>
        /// when unset (e.g. local development outside the action).
        /// </summary>
        public const string ParallelIndexVariableName = "PARTICULAR_RUN_TESTS_ACTION_PARALLEL_INDEX";

        /// <summary>
        /// Spacing between per-run ports. Wide enough that a run's embedded server has room for any
        /// additional listeners it opens alongside its main port.
        /// </summary>
        public const int ParallelPortSpacing = 10;

        /// <summary>
        /// Returns the port derived from the run-tests-action per-run parallel index.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The action sets <see cref="ParallelIndexVariableName"/> on every spawned <c>dotnet test</c>
        /// process. Each run's port is computed as <c>startPort + (index * <see cref="ParallelPortSpacing"/>)</c>,
        /// so concurrent runs bind distinct ports (the historic spacing of 10 is preserved). A sequential
        /// run (index <c>0</c>) lands on <paramref name="startPort"/> -- the same base the historic probe
        /// started from, so non-parallel behavior is unchanged.
        /// </para>
        /// <para>
        /// When the index is unset (local development outside the action) it defaults to <c>0</c> and the
        /// base <paramref name="startPort"/> is used directly. <see cref="FindAvailablePort"/> remains
        /// available for callers that want to probe for a free port rather than derive a fixed one.
        /// </para>
        /// </remarks>
        public static int GetAssignedOrAvailablePort(int startPort)
        {
            var indexText = Environment.GetEnvironmentVariable(ParallelIndexVariableName);
            var index = int.TryParse(indexText?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : 0;
            return startPort + (Math.Max(0, index) * ParallelPortSpacing);
        }

        public static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }
    }
}