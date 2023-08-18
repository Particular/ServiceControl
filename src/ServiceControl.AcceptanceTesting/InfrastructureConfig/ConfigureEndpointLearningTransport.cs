namespace ServiceControl.AcceptanceTesting.InfrastructureConfig
{
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using Transports.Learning;

    public class ConfigureEndpointLearningTransport : ITransportIntegration
    {
        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            Directory.CreateDirectory(ConnectionString);

            var transportConfig = configuration.UseTransport<LearningTransport>();
            transportConfig.StorageDirectory(ConnectionString);
            transportConfig.NoPayloadSizeRestriction();

            return Task.FromResult(0);
        }

        public Task Cleanup()
        {
            if (Directory.Exists(ConnectionString))
            {
                Directory.Delete(ConnectionString, true);
            }

            return Task.FromResult(0);
        }

        static string Hash(string input)
        {
            using (var sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }

        public string Name => "Learning";
        public string TypeName => $"{typeof(LearningTransportCustomization).AssemblyQualifiedName}";
        public string ConnectionString { get; set; } = Path.Combine(TestContext.CurrentContext.TestDirectory, @"..\..\..\.transport\" + Hash(TestContext.CurrentContext.Test.FullName).Substring(0, 7));
    }
}