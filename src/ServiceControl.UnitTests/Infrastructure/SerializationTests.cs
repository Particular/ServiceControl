namespace ServiceControl.UnitTests.Infrastructure
{
    using NUnit.Framework;
    using ServiceControl.Infrastructure.WebApi;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    [TestFixture]
    public class SerializationTests
    {
        [Test]
        [SetCulture("tr-TR")]
        public void Should_use_invariant_culture_when_converting_property_names_to_underscore()
        {
            var expected = "{\"message_id\":\"1234\"}";

            var messages = CreateMessages();

            var actual = JsonSerializer.Serialize(messages, SerializerOptions.Default);

            Assert.That(actual, Is.EqualTo(expected));
        }

        DTO CreateMessages() =>
            new()
            {
                MessageId = "1234"
            };

        class DTO
        {
            public string MessageId { get; set; }
        }
    }
}