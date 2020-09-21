using System.Globalization;
using Raven.Client;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
using ServiceControl.Contracts.MessageFailures;
using ServiceControl.MessageFailures.Api;

namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Raven.Client.Documents;

    class UnArchiveMessagesByRangeHandler : IHandleMessages<UnArchiveMessagesByRange>
    {
        public UnArchiveMessagesByRangeHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(UnArchiveMessagesByRange message, IMessageHandlerContext context)
        {
            var options = new QueryOperationOptions
            {
                AllowStale = true
            };
            var result = await store.Operations.SendAsync(new PatchByQueryOperation(
                new IndexQuery()
                {
                    Query = $@"from index {new FailedMessageViewIndex().IndexName} as i where i.Status=$status AND i.LastModified between $startDate and $endDate 
                  update
                  {{
                      i.Status=$newStatus
                  }}",
                    QueryParameters = new Parameters()
                    {
                        {"startDate", message.From.Ticks},
                        {"endDate", message.To.Ticks},
                        {"status", (int)FailedMessageStatus.Archived},
                        {"newStatus", (int)FailedMessageStatus.Unresolved}
                    }
                }, options)).ConfigureAwait(true);

            var completion = await result.WaitForCompletionAsync<BulkOperationResult>().ConfigureAwait(false);

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                MessagesCount = completion.DocumentsProcessed
            }).ConfigureAwait(false);
        }

        IDocumentStore store;
        IDomainEvents domainEvents;
        class DocumentPatchResult
        {
            public string Document { get; set; }
        }
    }
}