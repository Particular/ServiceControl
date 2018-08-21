namespace ServiceControl.UnitTests.Recoverability
{
    using System.Collections.Generic;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    class CorruptedReplyToHeaderStrategyTests
    {
        [Test]
        public void Handle_corrupted_header()
        {
            // Arrange
            var strategy = new CorruptedReplyToHeaderStrategy(ServiceControlMachineName);

            var corruptedReplyToAddress = $"SomeEndpoint@{ServiceControlMachineName}";

            var headers = new Dictionary<string, string>
            {
                [Headers.ReplyToAddress] = corruptedReplyToAddress,
                [Headers.OriginatingMachine] = SendingMachineName
            };

            // Act
            strategy.FixCorruptedReplyToHeader(headers);

            // Assert
            headers.AssertHeader(Headers.ReplyToAddress, $"SomeEndpoint@{SendingMachineName}");
            headers.AssertHeader("ServiceControl.OldReplyToAddress", corruptedReplyToAddress);
        }

        [Test]
        public void Handle_non_corupted_header()
        {
            // Arrange
            var strategy = new CorruptedReplyToHeaderStrategy(ServiceControlMachineName);

            var nonCorruptedReplyToAddress = $"SomeEndpoint@{SendingMachineName}";

            var headers = new Dictionary<string, string>
            {
                [Headers.ReplyToAddress] = nonCorruptedReplyToAddress,
                [Headers.OriginatingMachine] = SendingMachineName
            };

            // Act
            strategy.FixCorruptedReplyToHeader(headers);

            // Assert
            headers.AssertHeader(Headers.ReplyToAddress, $"SomeEndpoint@{SendingMachineName}");
            headers.AssertHeaderMissing("ServiceControl.OldReplyToAddress");
        }

        [Test]
        public void Handle_no_OriginatingMachine()
        {
            // Arrange
            var strategy = new CorruptedReplyToHeaderStrategy(ServiceControlMachineName);

            var maybeCorruptedReplyToAddress = $"SomeEndpoint@{ServiceControlMachineName}";

            var headers = new Dictionary<string, string>
            {
                [Headers.ReplyToAddress] = maybeCorruptedReplyToAddress
            };

            // Act
            strategy.FixCorruptedReplyToHeader(headers);

            // Assert
            headers.AssertHeader(Headers.ReplyToAddress, maybeCorruptedReplyToAddress);
            headers.AssertHeaderMissing("ServiceControl.OldReplyToAddress");
        }

        [Test]
        public void Handle_no_machine_name_in_header()
        {
            // Arrange
            var strategy = new CorruptedReplyToHeaderStrategy(ServiceControlMachineName);

            var replyToAddressWithNoMachineName = "SomeEndpoint";

            var headers = new Dictionary<string, string>
            {
                [Headers.ReplyToAddress] = replyToAddressWithNoMachineName,
                [Headers.OriginatingMachine] = SendingMachineName
            };

            // Act
            strategy.FixCorruptedReplyToHeader(headers);

            // Assert
            headers.AssertHeader(Headers.ReplyToAddress, replyToAddressWithNoMachineName);
            headers.AssertHeaderMissing("ServiceControl.OldReplyToAddress");
        }

        private const string ServiceControlMachineName = nameof(ServiceControlMachineName);
        private const string SendingMachineName = nameof(SendingMachineName);
    }
}