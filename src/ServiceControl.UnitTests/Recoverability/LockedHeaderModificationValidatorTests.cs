namespace ServiceControl.UnitTests.Recoverability
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using MessageFailures.Api;

    [TestFixture]
    public class LockedHeaderModificationValidatorTests
    {
        LockedHeaderModificationValidator validator;
        string[] lockedHeaders;
        Dictionary<string, string> editedMessageHeaders;
        Dictionary<string, string> originalMessageHeaders;

        [SetUp]
        public void Setup()
        {
            validator = new LockedHeaderModificationValidator();
        }

        [Test]
        public void Headers_not_in_the_locked_list_should_not_be_treated_as_a_modification()
        {
            lockedHeaders = new[] { "NServiceBus.MessageId" };
            editedMessageHeaders = new Dictionary<string, string>{{"foo", "asdf"}};
            originalMessageHeaders = new Dictionary<string, string> { {"foo", "blah"} };
            Assert.IsFalse(validator.Check(lockedHeaders, editedMessageHeaders, originalMessageHeaders));
        }

        [Test]
        public void No_header_on_the_original_message_should_not_be_treated_as_a_modification()
        {
            lockedHeaders = new [] { "NServiceBus.MessageId" };
            editedMessageHeaders = new Dictionary<string, string>{{"NServiceBus.MessageId","asdf"}};
            originalMessageHeaders = new Dictionary<string, string>();
            Assert.IsFalse(validator.Check(lockedHeaders, editedMessageHeaders, originalMessageHeaders));
        }

        [Test]
        public void No_header_on_the_new_message_should_be_treated_as_a_modification()
        {
            lockedHeaders = new[] { "NServiceBus.MessageId" };
            editedMessageHeaders = new Dictionary<string, string> { {"foo", "bar"} };
            originalMessageHeaders = new Dictionary<string, string>{{"NServiceBus.MessageId", "asdf"}};
            Assert.IsTrue(validator.Check(lockedHeaders, editedMessageHeaders, originalMessageHeaders));
        }

        [Test]
        public void Header_with_different_value_should_be_treated_as_a_modification()
        {
            lockedHeaders = new[] { "NServiceBus.MessageId" };
            editedMessageHeaders = new Dictionary<string, string> { {"NServiceBus.MessageId", "bar"} };
            originalMessageHeaders = new Dictionary<string, string> { { "NServiceBus.MessageId", "asdf" } };
            Assert.IsTrue(validator.Check(lockedHeaders, editedMessageHeaders, originalMessageHeaders));
        }

        [Test]
        public void Header_with_same_value_but_different_casing_should_be_treated_as_a_modification()
        {
            lockedHeaders = new[] { "NServiceBus.MessageId" };
            editedMessageHeaders = new Dictionary<string, string> {{"NServiceBus.MessageId", "asdf"} };
            originalMessageHeaders = new Dictionary<string, string> { { "NServiceBus.MessageId", "ASDF" } };
            Assert.IsTrue(validator.Check(lockedHeaders, editedMessageHeaders, originalMessageHeaders));
        }

        [Test]
        public void Header_with_same_key_but_different_casing_should_be_treated_as_a_modification()
        {
            lockedHeaders = new[] { "NServiceBus.MessageId" };
            editedMessageHeaders = new Dictionary<string, string> { {"nservicebus.messageid", "asdf"} };
            originalMessageHeaders = new Dictionary<string, string> { { "NServiceBus.MessageId", "asdf" } };
            Assert.IsTrue(validator.Check(lockedHeaders, editedMessageHeaders, originalMessageHeaders));
        }
    }
}