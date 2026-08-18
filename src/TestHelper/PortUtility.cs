namespace TestHelper
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net.NetworkInformation;

    public static class PortUtility
    {
        /// <summary>
        /// The port an embedded server should bind, when the test runner has assigned one.
        /// </summary>
        public const string AssignedPortVariableName = "ServiceControl_TESTS_RAVENDB_PORT";

        /// <summary>
        /// Returns the port assigned by the test runner, or probes for a free one when running alone.
        /// </summary>
        /// <remarks>
        /// Concurrent test processes cannot each probe: <see cref="FindAvailablePort"/> only inspects
        /// the listeners active at that instant, so processes starting together all see the same port
        /// free and all but one then fail to bind.
        /// </remarks>
        public static int GetAssignedOrAvailablePort(int startPort)
        {
            var assignedPort = Environment.GetEnvironmentVariable(AssignedPortVariableName);

            return string.IsNullOrWhiteSpace(assignedPort)
                ? FindAvailablePort(startPort)
                : int.Parse(assignedPort, CultureInfo.InvariantCulture);
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
