namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using NServiceBus;

    public static class Helper
    {
        public static void ConfigureNameShorteners(this AzureServiceBusTransport transport)
        {
            transport.SubscriptionNamingConvention = n => n.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n) : n;
            transport.SubscriptionRuleNamingConvention = n => n.FullName.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n.FullName) : n.FullName;
        }

        const int MaxEntityName = 50;

        static class MD5DeterministicNameBuilder
        {
            public static string Build(string input)
            {
                var inputBytes = Encoding.Default.GetBytes(input);

                // use MD5 hash to get a 16-byte hash of the string
                var hashBytes = MD5.HashData(inputBytes);

                // generate a guid from the hash:
                return new Guid(hashBytes).ToString();
            }
        }
    }
}