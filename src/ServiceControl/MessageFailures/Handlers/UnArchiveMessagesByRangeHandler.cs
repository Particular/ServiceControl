namespace ServiceControl.MessageFailures.Handlers
{
    using System.Globalization;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class UnArchiveMessagesByRangeHandler : IHandleMessages<UnArchiveMessagesByRange>
    {
        private readonly IDocumentStore store;
        private readonly IBus bus;

        public UnArchiveMessagesByRangeHandler(IDocumentStore store, IBus bus)
        {
            this.store = store;
            this.bus = bus;
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
                }, new BulkOperationOptions {AllowStale = true }).WaitForCompletion();

            var patchedDocumentIds = result.JsonDeserialization<DocumentPatchResult[]>();

            bus.Publish<FailedMessagesUnArchived>(m =>
            {
                m.MessagesCount = patchedDocumentIds.Length;
            });
        }

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }
    }
}