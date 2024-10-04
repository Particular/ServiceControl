namespace ServiceControl.Transport.Tests
{
    using NUnit.Framework;

    [TestFixture]
    class ASQTransportTests : TransportTestFixture
    {
        [TestCase(15)]
        [TestCase(null)]
        public void Should_set_max_concurrency_for_audit(int? setConcurrency)
        {
            var (transportSettings, _) = SetupAndCustomizeInstance(
                transportSettings => transportSettings.MaxConcurrency = setConcurrency,
                (transportCustomization, endpointConfiguration, transportSettings) => transportCustomization.CustomizeAuditEndpoint(endpointConfiguration, transportSettings));

            Assert.That(transportSettings.MaxConcurrency, Is.EqualTo(setConcurrency ?? 32));
        }

        [TestCase(15)]
        [TestCase(null)]
        public void Should_set_max_concurrency_for_monitoring(int? setConcurrency)
        {
            var (transportSettings, _) = SetupAndCustomizeInstance(
                transportSettings => transportSettings.MaxConcurrency = setConcurrency,
                (transportCustomization, endpointConfiguration, transportSettings) => transportCustomization.CustomizeMonitoringEndpoint(endpointConfiguration, transportSettings));

            Assert.That(transportSettings.MaxConcurrency, Is.EqualTo(setConcurrency ?? 32));
        }

        [TestCase(15)]
        [TestCase(null)]
        public void Should_set_max_concurrency_for_primary(int? setConcurrency)
        {
            var (transportSettings, _) = SetupAndCustomizeInstance(
                transportSettings => transportSettings.MaxConcurrency = setConcurrency,
                (transportCustomization, endpointConfiguration, transportSettings) => transportCustomization.CustomizePrimaryEndpoint(endpointConfiguration, transportSettings));

            Assert.That(transportSettings.MaxConcurrency, Is.EqualTo(setConcurrency ?? 10));
        }
    }
}
