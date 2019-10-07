namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Newtonsoft.Json;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.WebApi;

    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void Should_use_invariant_culture_when_converting_property_names_to_underscore()
        {
            var expected = "{\"message_id\":\"1234\"}";

            using (new DisposableCulture("tr-TR"))
            {
                var serializer = JsonSerializer.Create(JsonNetSerializerSettings.CreateDefault());
                var stream = new MemoryStream();
                var messages = CreateMessages();

                using (var writer = new JsonTextWriter(new StreamWriter(stream)))
                {
                    serializer.Serialize(writer, messages);
                }

                Assert.AreEqual(expected, Encoding.Default.GetString(stream.ToArray()));
            }
        }

        private DTO CreateMessages()
        {
            return new DTO
            {
                MessageId = "1234"
            };
        }

        private class DTO
        {
            public string MessageId { get; set; }
        }
    }

    public class DisposableCulture : IDisposable
    {
        public DisposableCulture(string culture)
        {
            oldCulture = CultureInfo.CurrentCulture;
            oldUICulture = CultureInfo.CurrentUICulture;

            Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = oldCulture;
            Thread.CurrentThread.CurrentUICulture = oldUICulture;
        }

        private CultureInfo oldCulture;
        private CultureInfo oldUICulture;
    }
}