namespace Particular.Backend.Debugging.UnitTests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.Backend.Debugging.Enrichers;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    [TestFixture]
    public class FailedMessageImporterTests
    {
        [Test]
        public void It_marks_snapshot_as_failure_after_processing_a_failure_notification()
        {
            var snapshotStore = new FakeMessageSnapshotStore();
            var importer = new FailedMessageImporter(snapshotStore, new SnapshotUpdater(new IEnrichAuditMessageSnapshots[] { }));

            importer.ProcessFailed(new IngestedMessage("1", "1", true, new byte[0], new HeaderCollection(new Dictionary<string, string>()), MessageType.Unknown,
                EndpointInstance.Unknown, EndpointInstance.Unknown));

            Assert.AreEqual(MessageStatus.Failed, snapshotStore.Snapshot.Status);
        }
        
        [Test]
        public void It_uses_time_of_failure_header_as_last_attempt_date()
        {
            var snapshotStore = new FakeMessageSnapshotStore();
            var importer = new FailedMessageImporter(snapshotStore, new SnapshotUpdater(new IEnrichAuditMessageSnapshots[] { }));

            var headers = new Dictionary<string, string>();
            var failureTime = new DateTime(2015, 2, 3, 7, 30, 15, DateTimeKind.Utc);
            headers["NServiceBus.TimeOfFailure"] = DateTimeExtensions.ToWireFormattedString(failureTime);
            importer.ProcessFailed(new IngestedMessage("1", "1", true, new byte[0], new HeaderCollection(headers), MessageType.Unknown,
                EndpointInstance.Unknown, EndpointInstance.Unknown));

            Assert.AreEqual(failureTime, snapshotStore.Snapshot.AttemptedAt);
        }

        [Test]
        public void It_marks_snapshot_as_repeated_failure_after_processing_a_subsequent_failure_notification()
        {
            var snapshotStore = new FakeMessageSnapshotStore();
            var importer = new FailedMessageImporter(snapshotStore, new SnapshotUpdater(new IEnrichAuditMessageSnapshots[] {}));
            importer.ProcessFailed(new IngestedMessage("1","1",true,new byte[0], new HeaderCollection(new Dictionary<string, string>()), MessageType.Unknown,
                EndpointInstance.Unknown, EndpointInstance.Unknown));

            importer.ProcessFailed(new IngestedMessage("2", "2", true, new byte[0], new HeaderCollection(new Dictionary<string, string>()), MessageType.Unknown,
                EndpointInstance.Unknown, EndpointInstance.Unknown));

            Assert.AreEqual(MessageStatus.RepeatedFailure, snapshotStore.Snapshot.Status);
        }

        private class FakeMessageSnapshotStore : IStoreMessageSnapshots
        {
            MessageSnapshot snapshot;

            public MessageSnapshot Snapshot
            {
                get { return snapshot; }
            }

            public void StoreOrUpdate(string uniqueId, Action<MessageSnapshot> initializeNewCallback, Action<MessageSnapshot> updateCallback)
            {
                if (Snapshot == null)
                {
                    snapshot = new MessageSnapshot();
                    initializeNewCallback(Snapshot);
                }
                else
                {
                    updateCallback(Snapshot);
                }
            }

            public void UpdateIfExists(string uniqueId, Action<MessageSnapshot> updateCallback)
            {
            }
        }
    }
}
