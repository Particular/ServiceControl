namespace ServiceControl.Transport.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class TransportTestFixture
    {
        [SetUp]
        public virtual Task Setup()
        {
            configuration = new TransportTestsConfiguration();

            return configuration.Configure();
        }

        [TearDown]
        public virtual Task Cleanup()
        {
            return configuration?.Cleanup();
        }

        protected TransportTestsConfiguration configuration;
    }
}