namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations.BodyStorage;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        private static ILog logger = LogManager.GetLogger<ArchiveAllInGroupHandler>();

        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly IMessageBodyStore messageBodyStore;

        public ArchiveAllInGroupHandler(IBus bus, IDocumentStore store, IMessageBodyStore messageBodyStore)
        {
            this.bus = bus;
            this.store = store;
            this.messageBodyStore = messageBodyStore;
        }

        public void Handle(ArchiveAllInGroup message)
        {
            logger.Info($"Archiving of {message.GroupId} started");

            FailedMessage.FailureGroup failureGroup;
            DocumentPatchResult[] patchedDocumentIds;

            using (var session = store.OpenSession())
            {
                var result = session.Advanced.DocumentStore.DatabaseCommands.UpdateByIndex(
                    new FailedMessages_ByGroup().IndexName,
                    new IndexQuery
                    {
                        Query = $"FailureGroupId:{message.GroupId} AND Status:{(int) FailedMessageStatus.Unresolved}",
                        Cutoff = message.CutOff
                    },
                    new[]
                    {
                        new PatchRequest
                        {
                            Type = PatchCommandType.Set,
                            Name = "Status",
                            Value = (int) FailedMessageStatus.Archived
                        }
                    }, new BulkOperationOptions
                    {
                        AllowStale = true
                    }).WaitForCompletion();

                patchedDocumentIds = result.JsonDeserialization<DocumentPatchResult[]>();
                logger.Info($"Archiving of {message.GroupId} ended");
                logger.Info($"Archived {patchedDocumentIds.Length} for {message.GroupId}");

                if (patchedDocumentIds.Length == 0)
                {
                    return;
                }

                var messageBodyIds = patchedDocumentIds.Select(x => x.Document.Split('/').LastOrDefault()).Where(x => x != null);
                MarkMessageBodiesAsTransient(messageBodyIds);

                var failedMessage = session.Load<FailedMessage>(patchedDocumentIds[0].Document);
                failureGroup = failedMessage.FailureGroups.FirstOrDefault();
            }

            var groupName = "Undefined";

            if (failureGroup?.Title != null)
            {
                groupName = failureGroup.Title;
            }

            bus.Publish(new FailedMessageGroupArchived
            {
                GroupId = message.GroupId,
                GroupName = groupName,
                MessagesCount = patchedDocumentIds.Length
            });
        }

        private void MarkMessageBodiesAsTransient(IEnumerable<string> messageBodyIds)
        {
            foreach (var messageBodyId in messageBodyIds)
            {
                messageBodyStore.ChangeTag(messageBodyId, BodyStorageTags.ErrorPersistent, BodyStorageTags.ErrorTransient);
            }
        }

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }
    }
}
