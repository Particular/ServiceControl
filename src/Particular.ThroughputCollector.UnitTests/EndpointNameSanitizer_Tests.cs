namespace Particular.ThroughputCollector.UnitTests
{
    using System.Linq;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Shared;

    [TestFixture]
    public class EndpointNameSanitizer_Tests
    {
        [Test]
        public void Should_remove_invalid_characters_for_ASQS()
        {
            var endpointName = "MyInvalid#$Name234&*(";
            var sanitizedName = EndpointNameSanitizer.SanitizeEndpointName(endpointName, Contracts.Broker.AmazonSQS);

            Assert.That(sanitizedName, Is.Not.Null);
            Assert.That(sanitizedName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'), Is.True);
        }
    }
}