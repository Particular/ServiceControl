﻿namespace ServiceControlInstaller.Engine.Ports
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

            return PortNotInUse(portNumber);
        }
        public static bool PortNotInUse(int portNumber)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            return ipGlobalProperties.GetActiveTcpListeners().All(p => p.Port != portNumber) &&
                   ipGlobalProperties.GetActiveTcpConnections().All(p => p.LocalEndPoint.Port != portNumber);
        }
    }
}