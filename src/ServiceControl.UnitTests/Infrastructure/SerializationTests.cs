namespace ServiceControl.UnitTests.Infrastructure
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Nancy;

    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void Should_use_invariant_culture_when_converting_property_names_to_underscore()
        {
            var expected = "{\"message_id\":\"1234\"}";

            using (new DisposableCulture("tr-TR"))
            {
                var serializer = new JsonNetSerializer();
                var stream = new MemoryStream();
                var messages = CreateMessages();

                serializer.Serialize("application/json", messages, stream);

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
        private CultureInfo oldCulture;
        private CultureInfo oldUICulture;

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
    }
}