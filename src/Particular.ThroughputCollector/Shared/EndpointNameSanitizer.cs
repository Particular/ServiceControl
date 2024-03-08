namespace Particular.ThroughputCollector.Shared
{
    using System.Text;

    static class EndpointNameSanitizer
    {
        public static string SanitizeEndpointName(string endpointName, Contracts.Broker broker)
        {
#pragma warning disable IDE0072 // Add missing cases
            return broker switch
            {
                Contracts.Broker.AmazonSQS => SanitizeForAmazonSQS(endpointName),
                Contracts.Broker.SqlServer => SanitizeForSqlServer(endpointName),
                Contracts.Broker.AzureServiceBus => SanitizeForAzureServiceBus(endpointName),
                _ => endpointName,
            };
#pragma warning restore IDE0072 // Add missing cases
        }

        static string SanitizeForAmazonSQS(string endpointName)
        {
            if (string.IsNullOrWhiteSpace(endpointName))
            {
                return endpointName;
            }

            var s = endpointName;
            var skipCharacters = s.EndsWith(".fifo") ? 5 : 0;
            var queueNameBuilder = new StringBuilder(s);

            // SQS queue names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < queueNameBuilder.Length - skipCharacters; ++i)
            {
                var c = queueNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    queueNameBuilder[i] = '-';
                }
            }

            return queueNameBuilder.ToString();
        }

        static string SanitizeForSqlServer(string endpointName)
        {
            return endpointName;
        }

        static string SanitizeForAzureServiceBus(string endpointName)
        {
            return endpointName;
        }
    }
}
