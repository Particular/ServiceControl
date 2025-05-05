using System;
using Amazon;
using Microsoft.Extensions.Configuration;

namespace JustSaying.Sample.Restaurant.KitchenConsole
{
    public static class ConfigurationExtensions
    {
        private const string AWSServiceUrlKey = "AWSServiceUrl";

        private const string AWSRegionKey = "AWSRegion";

        public static bool HasAWSServiceUrl(this IConfiguration configuration)
        {
            return !string.IsNullOrWhiteSpace(configuration[AWSServiceUrlKey]);
        }

        public static Uri GetAWSServiceUri(this IConfiguration configuration)
        {
            return new Uri(configuration[AWSServiceUrlKey]);
        }

        public static RegionEndpoint GetAWSRegion(this IConfiguration configuration)
        {
            return RegionEndpoint.GetBySystemName(configuration[AWSRegionKey]);
        }
    }
}
