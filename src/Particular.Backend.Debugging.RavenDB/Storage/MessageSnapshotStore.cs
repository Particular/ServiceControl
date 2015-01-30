namespace Particular.Backend.Debugging.RavenDB.Storage
{
    using System;
    using Raven.Client;

    public class MessageSnapshotStore : IStoreMessageSnapshots
    {
        readonly IDocumentStore documentStore;

        public MessageSnapshotStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public void StoreOrUpdate(string uniqueId, Action<AuditMessageSnapshot> initializeNewCallback, Action<AuditMessageSnapshot> updateCallback)
        {
            using (var session = documentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                var documentId = MakeDocumentId(uniqueId);
                var snapshotDocument = session.Load<MessageSnapshotDocument>(documentId);
                if (snapshotDocument != null)
                {
                    updateCallback(snapshotDocument);
                }
                else
                {
                    snapshotDocument = new MessageSnapshotDocument
                    {
                        Id = documentId
                    };
                    initializeNewCallback(snapshotDocument);
                }
                session.Store(snapshotDocument);
                session.SaveChanges();
            }
        }

        public void UpdateIfExists(string uniqueId, Action<AuditMessageSnapshot> updateCallback)
        {
            using (var session = documentStore.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true;
                var documentId = MakeDocumentId(uniqueId);
                var snapshotDocument = session.Load<MessageSnapshotDocument>(documentId);
                if (snapshotDocument == null)
                {
                    return;
                }
                updateCallback(snapshotDocument);
                session.Store(snapshotDocument);
                session.SaveChanges();
            }
        }


        static string MakeDocumentId(string messageUniqueId)
        {
            return "AuditMessageSnapshots/" + messageUniqueId;
        }

    }
}