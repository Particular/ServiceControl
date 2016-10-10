namespace ServiceControl.MessageFailures.Handlers
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;
    using ServiceControl.Operations.BodyStorage;

    public class UnArchiveMessagesByRangeHandler : IHandleMessages<UnArchiveMessagesByRange>
    {
        private readonly IDocumentStore store;
        private readonly IBus bus;
        private readonly IMessageBodyStore messageBodyStore;

        public UnArchiveMessagesByRangeHandler(IDocumentStore store, IBus bus, IMessageBodyStore messageBodyStore)
        {
            this.store = store;
            this.bus = bus;
            this.messageBodyStore = messageBodyStore;
        }

        public void Handle(UnArchiveMessagesByRange message)
        {
            var result = store.DatabaseCommands.UpdateByIndex(
                new FailedMessageViewIndex().IndexName,
                new IndexQuery
                {
                    Query = string.Format(CultureInfo.InvariantCulture, "LastModified:[{0} TO {1}] AND Status:{2}", message.From.Ticks, message.To.Ticks, (int) FailedMessageStatus.Archived),
                    Cutoff = message.CutOff
                }, new ScriptedPatchRequest
                {
                    Script = @"
if(this.Status === archivedStatus) {
    this.Status = unresolvedStatus;
}
",
                    Values =
                    {
                        {"archivedStatus", (int) FailedMessageStatus.Archived},
                        {"unresolvedStatus", (int) FailedMessageStatus.Unresolved}
                    }
                }, new BulkOperationOptions
                {
                    AllowStale = true,
                    RetrieveDetails = true,
                    MaxOpsPerSec = 700
                }).WaitForCompletion();

            var patchedDocumentIds = result.JsonDeserialization<DocumentPatchResult[]>();

            var messageBodyIds = patchedDocumentIds.Select(x => x.Document.Split('/').LastOrDefault()).Where(x => x != null);
            MarkMessageBodiesAsPersistent(messageBodyIds);

            bus.Publish<FailedMessagesUnArchived>(m =>
            {
                m.MessagesCount = patchedDocumentIds.Length;
            });
        }

        private void MarkMessageBodiesAsPersistent(IEnumerable<string> messageBodyIds)
        {
            foreach (var messageBodyId in messageBodyIds)
            {
                messageBodyStore.ChangeTag(messageBodyId, BodyStorageTags.ErrorTransient, BodyStorageTags.ErrorPersistent);
            }
        }

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }
    }
}