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

        [TestCase(true)]
        [TestCase(false)]
        public void An_empty_body_has_no_body(bool hasExternalStorage)
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, ReadOnlyMemory<byte>.Empty, Max, hasExternalStorage);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.None));
                Assert.That(text, Is.Null);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Small_text_stays_inline(bool hasExternalStorage)
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, Encoding.UTF8.GetBytes("{}"), Max, hasExternalStorage);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.Stored));
                Assert.That(text, Is.EqualTo("{}"));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Text_at_the_limit_stays_inline(bool hasExternalStorage)
        {
            var (state, _) = MessageBodyClassifier.Classify(Json, Encoding.UTF8.GetBytes(new string('a', Max)), Max, hasExternalStorage);

            Assert.That(state, Is.EqualTo(BodyState.Stored));
        }

        [Test]
        public void Text_over_the_limit_goes_external_and_keeps_a_prefix()
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, Encoding.UTF8.GetBytes(new string('a', Max + 1)), Max, hasExternalStorage: true);

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

            var (state, text) = MessageBodyClassifier.Classify(Json, body, Max, hasExternalStorage: true);

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
            var (state, text) = MessageBodyClassifier.Classify(new Dictionary<string, string> { [Headers.ContentType] = contentType }, new byte[] { 1, 2, 3 }, Max, hasExternalStorage: true);

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

            var (state, _) = MessageBodyClassifier.Classify(headers, Encoding.UTF8.GetBytes("text"), Max, hasExternalStorage: true);

            Assert.That(state, Is.EqualTo(BodyState.External));
        }

        [Test]
        public void A_body_that_is_not_utf8_goes_external()
        {
            var (state, text) = MessageBodyClassifier.Classify(new Dictionary<string, string>(), new byte[] { 0xC3, 0x28 }, Max, hasExternalStorage: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.External));
                Assert.That(text, Is.Null);
            }
        }

        [Test]
        public void A_body_with_a_nul_goes_external()
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, "a\0b"u8.ToArray(), Max, hasExternalStorage: true);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.External));
                Assert.That(text, Is.Null);
            }
        }

        [Test]
        public void Without_external_storage_text_over_the_limit_is_dropped_with_a_prefix()
        {
            var (state, text) = MessageBodyClassifier.Classify(Json, Encoding.UTF8.GetBytes(new string('a', Max + 1)), Max, hasExternalStorage: false);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.TooLarge));
                Assert.That(text, Is.EqualTo(new string('a', Max)));
            }
        }

        [Test]
        public void Without_external_storage_binary_content_is_not_text()
        {
            var (state, text) = MessageBodyClassifier.Classify(new Dictionary<string, string> { [Headers.ContentType] = "application/octet-stream" }, new byte[] { 1, 2, 3 }, Max, hasExternalStorage: false);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.NotText));
                Assert.That(text, Is.Null);
            }
        }

        [Test]
        public void Without_external_storage_binary_content_over_the_limit_is_too_large()
        {
            var (state, text) = MessageBodyClassifier.Classify(new Dictionary<string, string> { [Headers.ContentType] = "application/octet-stream" }, new byte[Max + 1], Max, hasExternalStorage: false);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(state, Is.EqualTo(BodyState.TooLarge));
                Assert.That(text, Is.Null);
            }
        }
    }
}
