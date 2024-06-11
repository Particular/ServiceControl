namespace ServiceControl.Transport.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using NUnit.Framework.Internal;

    [TestFixture]
    class FullEndpointTestFixture
    {
        [SetUp]
        public virtual async Task Setup()
        {
            configuration = new TransportTestsConfiguration();
            var queueSuffix = $"-{System.IO.Path.GetRandomFileName().Replace(".", string.Empty)}";

            Conventions.EndpointNamingConvention = t =>
            {
                var classAndEndpoint = t.FullName.Split('.').Last();

                var endpointBuilder = classAndEndpoint.Split('+').Last();

                return endpointBuilder + queueSuffix;
            };

            await configuration.Configure();
        }

        [TearDown]
        public virtual async Task Cleanup()
        {
            if (configuration != null)
            {
                await configuration.Cleanup();
            }
        }

        protected TransportTestsConfiguration configuration;
    }
}