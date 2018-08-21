namespace ServiceControlInstaller.Engine.Ports
{
    using System;
    using System.Linq;
    using System.Net.NetworkInformation;

    public class PortUtils
    {
        public static bool CheckAvailable(int portNumber)
        {
            if (1 > portNumber || 49151 < portNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(portNumber), "Port number is not between 1 and 49151");
            }

            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            return ipGlobalProperties.GetActiveTcpListeners().All(p => p.Port != portNumber) &&
                   ipGlobalProperties.GetActiveTcpConnections().All(p => p.LocalEndPoint.Port != portNumber);
        }
    }
}