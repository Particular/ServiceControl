namespace TestHelper
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    public static class PortUtility
    {
        public static int FindAvailablePort(int startPort)
        {
            const int searchRange = 1024;

            // Multiple test hosts can start embedded RavenDB instances at the same time in CI.
            // Offset the initial probe per process to reduce cross-process collisions.
            var processOffset = Environment.ProcessId % searchRange;

            for (var attempt = 0; attempt < searchRange; attempt++)
            {
                var port = startPort + ((processOffset + attempt) % searchRange);

                try
                {
                    using var listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    return ((IPEndPoint)listener.LocalEndpoint).Port;
                }
                catch (SocketException)
                {
                    // Port is not currently available, try the next one.
                }
            }

            return startPort + processOffset;
        }
    }
}
