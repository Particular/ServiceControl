namespace ServiceControl.MessageFailures.Handlers
{
    using System.Globalization;
    using System.Threading.Tasks;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class UnArchiveMessagesByRangeHandler : IHandleMessages<UnArchiveMessagesByRange>
    {
        IDocumentStore store;
        IDomainEvents domainEvents;

        public UnArchiveMessagesByRangeHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(UnArchiveMessagesByRange message, IMessageHandlerContext context)
        {
            var options = new BulkOperationOptions
            {
                AllowStale = true
            };
            var result = await store.AsyncDatabaseCommands.UpdateByIndexAsync(
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
                }, options).ConfigureAwait(false);

            var patchedDocumentIds = (await result.WaitForCompletionAsync().ConfigureAwait(false))
                .JsonDeserialization<DocumentPatchResult[]>();

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                MessagesCount = patchedDocumentIds.Length
            }).ConfigureAwait(false);
        }

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }
    }
}