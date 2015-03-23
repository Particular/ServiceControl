namespace Particular.Backend.Debugging.RavenDB.Expiration
{
    using System.Linq;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.Operations;

    public class ExpiryProcessedMessageIndex : AbstractMultiMapIndexCreationTask
    {
        public ExpiryProcessedMessageIndex()
        {
            AddMap<MessageSnapshotDocument>(messages => from message in messages
                               where message.Status != MessageStatus.Failed && message.Status != MessageStatus.RepeatedFailure
                               select new
                               {
                                   ProcessedAt = message.ProcessedAt,
                               });
            AddMap<SagaSnapshot>(messages => from message in messages
                               select new
                               {
                                   ProcessedAt = message.ProcessedAt,
                               });

            DisableInMemoryIndexing = true;
        }
    }
}