namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    static class CheckMachineNameForCompliance
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool GetComputerNameEx(COMPUTER_NAME_FORMAT nameType, [Out] StringBuilder lpBuffer, ref uint lpnSize);

        /// <summary>
        /// Checks to see if the NetBios computer name exceeds 15 characters and if so throws an exception.
        /// </summary>
        public static void Check()
        {
            uint capacity = 24;
            var buffer = new StringBuilder((int)capacity);
            if (!GetComputerNameEx(COMPUTER_NAME_FORMAT.ComputerNameNetBIOS, buffer, ref capacity))
            {
                return;
            }
            var netbiosName = buffer.ToString();
            if (netbiosName.Length <= 15)
            {
                return;
            }
            throw new Exception($"The NetBIOS name {netbiosName} is longer than 15 characters. Shorten the machine name to 15 characters or less for MSMQ to deliver messages.");
        }

        enum COMPUTER_NAME_FORMAT
        {
            ComputerNameNetBIOS,
            ComputerNameDnsHostname,
            ComputerNameDnsDomain,
            ComputerNameDnsFullyQualified,
            ComputerNamePhysicalNetBIOS,
            ComputerNamePhysicalDnsHostname,
            ComputerNamePhysicalDnsDomain,
            ComputerNamePhysicalDnsFullyQualified
        }
    }
}