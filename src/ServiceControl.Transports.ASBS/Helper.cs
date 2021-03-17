namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using NServiceBus;

    public static class Helper
    {
        public static void ConfigureTransport(this TransportExtensions<AzureServiceBusTransport> transport, TransportSettings transportSettings, TransportTransactionMode transportTransactionMode)
        {
            transport.ConfigureNameShorteners();

            transport.Transactions(transportTransactionMode);
            transport.ConnectionString(transportSettings.ConnectionString);
        }

        public static void ConfigureNameShorteners(this TransportExtensions<AzureServiceBusTransport> transport)
        {
#pragma warning disable 618
            transport.SubscriptionNameShortener(n => n.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n) : n);
            transport.RuleNameShortener(n => n.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n) : n);
#pragma warning restore 618
        }

        const int MaxEntityName = 50;

        static class MD5DeterministicNameBuilder
        {
            public static string Build(string input)
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                // use MD5 hash to get a 16-byte hash of the string
                using (var provider = new MD5CryptoServiceProvider())
                {
                    var hashBytes = provider.ComputeHash(inputBytes);
                    return new Guid(hashBytes).ToString();
                }
            }
        }
    }
}