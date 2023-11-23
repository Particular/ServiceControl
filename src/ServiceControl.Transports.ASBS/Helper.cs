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
#pragma warning disable 618
            transport.SubscriptionNamingConvention = n => n.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n) : n;
            transport.SubscriptionRuleNamingConvention = n => n.FullName.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n.FullName) : n.FullName;
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