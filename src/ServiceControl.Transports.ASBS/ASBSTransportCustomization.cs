namespace ServiceControl.Transports.ASBS
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using NServiceBus;
    using NServiceBus.Raw;

    public class ASBSTransportCustomization : TransportCustomization
    {
        const int MaxEntityName = 50;

        public override void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();

            transport.SubscriptionNameShortener(n => n.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n) : n);
            transport.RuleNameShortener(n => n.Length > MaxEntityName ? MD5DeterministicNameBuilder.Build(n) : n);

            transport.ConfigureTransport(transportSettings);
        }

        public override void CustomizeRawEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
        {
            var transport = endpointConfig.UseTransport<AzureServiceBusTransport>();

            transport.ConfigureTransport(transportSettings);
        }

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