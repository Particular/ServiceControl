namespace ServiceControl.Persistence.Tests.RavenDB.DocumentIdGenerators
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Persistence.RavenDB;

    [TestFixture]
    class EventLogItemIdGeneratorTests
    {
        // Documents already in a customer's store carry ids of this shape, and RavenDB reads a document's id from metadata rather than
        // from a field, so a changed format orphans everything written before the upgrade.
        [Test]
        public void Document_id_keeps_this_shape()
        {
            var eventId = Guid.Parse("2f6d1b4e-8c3a-4d5f-9a1b-7e0c2d3f4a5b");

            var documentId = EventLogItemIdGenerator.MakeDocumentId("CustomChecks", "CustomCheckFailed", eventId);

            Assert.That(documentId, Is.EqualTo("EventLogItem/CustomChecks/CustomCheckFailed/2f6d1b4e-8c3a-4d5f-9a1b-7e0c2d3f4a5b"));
        }

        [Test]
        public void Event_id_is_recovered_from_a_legacy_document_id()
        {
            var legacyDocumentId = "EventLogItem/Recoverability/MessageFailed/a1b2c3d4-e5f6-4788-9a0b-1c2d3e4f5a6b";

            var eventId = EventLogItemIdGenerator.GetEventIdFromDocumentId(legacyDocumentId);

            Assert.That(eventId, Is.EqualTo(Guid.Parse("a1b2c3d4-e5f6-4788-9a0b-1c2d3e4f5a6b")));
        }

        [Test]
        public void Composing_then_recovering_returns_the_same_event_id()
        {
            var eventId = Guid.CreateVersion7();

            var recovered = EventLogItemIdGenerator.GetEventIdFromDocumentId(
                EventLogItemIdGenerator.MakeDocumentId("Monitoring", "EndpointStarted", eventId));

            Assert.That(recovered, Is.EqualTo(eventId));
        }
    }
}
