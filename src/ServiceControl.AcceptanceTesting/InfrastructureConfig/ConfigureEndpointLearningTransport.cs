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
        public ConfigureEndpointLearningTransport()
        {
            using (var sha1 = SHA1.Create())
            {
                var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(TestContext.CurrentContext.Test.FullName));
                var hash = string.Concat(hashBytes.Take(6).Select(b => b.ToString("x2")));
                var relativePath = Path.Combine(TestContext.CurrentContext.TestDirectory, @"..", "..", "..", ".transport", hash);
                ConnectionString = Path.GetFullPath(relativePath);
            }
        }

        public string ConnectionString { get; set; }

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
                try
                {
                    Directory.Delete(ConnectionString, true);
                }
                catch (DirectoryNotFoundException)
                {
                }
            }

            return Task.FromResult(0);
        }

        public string Name => "Learning";
        public string TypeName => $"{typeof(LearningTransportCustomization).AssemblyQualifiedName}";
    }
}