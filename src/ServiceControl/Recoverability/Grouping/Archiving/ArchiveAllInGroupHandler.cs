namespace ServiceControl.Recoverability
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.MessageFailures;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        private static ILog logger = LogManager.GetLogger<ArchiveAllInGroupHandler>();

        private readonly IBus bus;
        private readonly IDocumentStore store;

        public ArchiveAllInGroupHandler(IBus bus, IDocumentStore store)
        {
            this.bus = bus;
            this.store = store;
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
                    }, true).WaitForCompletion();

                patchedDocumentIds = result.JsonDeserialization<DocumentPatchResult[]>();
                logger.Info($"Archiving of {message.GroupId} ended");
                logger.Info($"Archived {patchedDocumentIds.Length} for {message.GroupId}");

                if (patchedDocumentIds.Length == 0)
                {
                    return;
                }

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

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }
    }
}
