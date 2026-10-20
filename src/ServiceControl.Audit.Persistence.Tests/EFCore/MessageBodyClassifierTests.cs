namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Entities;
    using ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;

    [TestFixture]
    class MessageBodyClassifierTests
    {
        const int Max = 100;

        static Dictionary<string, string> Json => new() { [Headers.ContentType] = "application/json" };

        [Test]
        public void An_empty_body_has_no_body()
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, ReadOnlyMemory<byte>.Empty, Max);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.None));
                Assert.That(text, Is.Null);
            }
        }

        [Test]
        public void Small_text_stays_inline()
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, Encoding.UTF8.GetBytes("{}"), Max);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.Stored));
                Assert.That(text, Is.EqualTo("{}"));
            }
        }

        [Test]
        public void Text_at_the_limit_stays_inline()
        {
            var (state, _) = MessageBodyClassifier.Classify(Json, Encoding.UTF8.GetBytes(new string('a', Max)), Max);

            Assert.That(state, Is.EqualTo(BodyState.Stored));
        }

        [Test]
        public void Text_over_the_limit_goes_external_and_keeps_a_prefix()
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, Encoding.UTF8.GetBytes(new string('a', Max + 1)), Max);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.External));
                Assert.That(text, Is.EqualTo(new string('a', Max)));
            }
        }

        [Test]
        public void The_prefix_does_not_split_a_multi_byte_character()
        {
            // 99 ASCII bytes then a 3 byte character that straddles the limit
            var body = Encoding.UTF8.GetBytes(new string('a', Max - 1) + "€");

            var (state, text) = MessageBodyClassifier.Classify(Json, body, Max);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.External));
                Assert.That(text, Is.EqualTo(new string('a', Max - 1)));
            }
        }

        [TestCase("application/octet-stream")]
        [TestCase("application/json; binary")]
        public void Binary_content_goes_external_without_a_prefix(string contentType)
        {
            var (state, text) = MessageBodyClassifier.Classify(new Dictionary<string, string> { [Headers.ContentType] = contentType }, new byte[] { 1, 2, 3 }, Max);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.External));
                Assert.That(text, Is.Null);
            }
        }

        [Test]
        public void Encoded_content_goes_external()
        {
            var headers = new Dictionary<string, string> { ["Content-Encoding"] = "gzip" };

            var (state, _) = MessageBodyClassifier.Classify(headers, Encoding.UTF8.GetBytes("text"), Max);

            Assert.That(state, Is.EqualTo(BodyState.External));
        }

        [Test]
        public void A_body_that_is_not_utf8_goes_external()
        {
            var (state, text) = MessageBodyClassifier.Classify(new Dictionary<string, string>(), new byte[] { 0xC3, 0x28 }, Max);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.External));
                Assert.That(text, Is.Null);
            }
        }

        [Test]
        public void A_body_with_a_nul_goes_external()
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, "a\0b"u8.ToArray(), Max);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.External));
                Assert.That(text, Is.Null);
            }
        }
    }
}
