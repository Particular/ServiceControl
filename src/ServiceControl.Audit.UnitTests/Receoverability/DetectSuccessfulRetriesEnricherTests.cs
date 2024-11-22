namespace ServiceControl.Audit.UnitTests.Receoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using Audit.Auditing;
    using Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Recoverability;

    [TestFixture]
    public class DetectSuccessfulRetriesEnricherTests
    {
        [Test]
        public void It_does_not_sent_acknowledgement_if_audit_comes_from_new_endpoint_version()
        {
            var enricher = new DetectSuccessfulRetriesEnricher();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.UniqueMessageId"] = "MyId",
                ["ServiceControl.Retry.AcknowledgementQueue"] = "ErrorQueue",
                ["ServiceControl.Retry.AcknowledgementSent"] = "true"
            };

            var outgoingCommands = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();
            var metadata = new Dictionary<string, object>();

            enricher.Enrich(new AuditEnricherContext(headers, outgoingCommands, transportOperations, metadata));

            Assert.That(outgoingCommands, Is.Empty);
            Assert.That(transportOperations, Is.Empty);
        }

        [Test]
        public void It_sends_legacy_command_if_retry_comes_from_old_ServiceControl()
        {
            var enricher = new DetectSuccessfulRetriesEnricher();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.UniqueMessageId"] = "MyId"
            };

            var outgoingCommands = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();
            var metadata = new Dictionary<string, object>();

            enricher.Enrich(new AuditEnricherContext(headers, outgoingCommands, transportOperations, metadata));

            Assert.That(outgoingCommands, Is.Not.Empty);
            Assert.That(outgoingCommands, Is.All.InstanceOf(typeof(MarkMessageFailureResolvedByRetry)));
            Assert.That(transportOperations, Is.Empty);
        }

        [Test]
        public void It_sends_acknowledgement_if_audit_comes_from_old_endpoint_version()
        {
            var enricher = new DetectSuccessfulRetriesEnricher();

            var headers = new Dictionary<string, string>
            {
                ["ServiceControl.Retry.UniqueMessageId"] = "MyId",
                ["ServiceControl.Retry.AcknowledgementQueue"] = "ErrorQueue"
            };

            var outgoingCommands = new List<ICommand>();
            var transportOperations = new List<TransportOperation>();
            var metadata = new Dictionary<string, object>();

            enricher.Enrich(new AuditEnricherContext(headers, outgoingCommands, transportOperations, metadata));

            Assert.That(outgoingCommands, Is.Empty);

            var ack = transportOperations.Single();
            Assert.That(ack.Message.Headers.ContainsKey("ServiceControl.Retry.Successful"), Is.True);
        }
    }
}