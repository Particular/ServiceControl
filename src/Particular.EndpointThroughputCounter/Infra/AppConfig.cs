namespace Particular.EndpointThroughputCounter.Infra
{
    using System.IO;
    using Microsoft.Extensions.Configuration;

    static class AppConfig
    {
        static readonly IConfigurationRoot config;

        static AppConfig()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddEnvironmentVariables();
            builder.AddJsonFile("local.settings.json", true);

            config = builder.Build();
        }

        public static T Get<T>(string key) => config.GetValue<T>(key);
    }
}
