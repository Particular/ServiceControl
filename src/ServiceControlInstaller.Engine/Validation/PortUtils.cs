namespace ServiceControlInstaller.Engine.Ports
{
    using System;
    using System.Linq;
    using System.Net.NetworkInformation;

    public class PortUtils
    {
        public static bool CheckAvailable(int portNumber)
        {
            if (portNumber is < 1 or > 49151)
            {
                throw new ArgumentOutOfRangeException(nameof(portNumber), "Port number is not between 1 and 49151");
            }

            return !IsPortInUse(portNumber);
        }

        public static bool IsPortInUse(int portNumber)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

            var portHasActiveListener = ipGlobalProperties.GetActiveTcpListeners().Any(p => p.Port == portNumber);

            var portHasActiveTcpConnection = ipGlobalProperties.GetActiveTcpConnections().Any(p => p.LocalEndPoint.Port == portNumber);

            return portHasActiveListener || portHasActiveTcpConnection;
        }
    }
}