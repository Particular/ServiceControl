namespace ServiceControl.UnitTests.BodyStorage
{
    using System.Collections.Generic;
    using NUnit.Framework;

    [TestFixture]
    public class HeadersIsBinaryTests
    {
        [Test]
        public void Should_be_binary_when_content_encoding_header_is_present()
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Encoding", "gzip" },
                { "NServiceBus.ContentType", "application/json" }
            };

            var result = headers.IsBinary();
            Assert.IsTrue(result);
        }

        [Test]
        [TestCase("text/xml")]
        [TestCase("text/plain")]
        [TestCase("application/atom+xml")]
        [TestCase("application/ld+json")]
        [TestCase("application/json")]
        [TestCase("application/json; charset=utf-8")]
        public void Should_be_text_content_typ(string contentType)
        {
            var headers = new Dictionary<string, string>
            {
                { "NServiceBus.ContentType", contentType }
            };

            var result = headers.IsBinary();
            Assert.IsFalse(result);
        }

        [Test]
        [TestCase("application/binary")]
        [TestCase("application/octet-stream")]
        [TestCase("application/bson")]
        [TestCase("application/x-protobuf; messageType=\"x.y.Z\"")]
        public void Should_be_binary_content_typ(string contentType)
        {
            var headers = new Dictionary<string, string>
            {
                { "NServiceBus.ContentType", contentType }
            };

            var result = headers.IsBinary();
            Assert.IsTrue(result);
        }
    }
}