namespace ServiceControl.Recoverability
{
    using System.Globalization;
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

        public void Handle(ArchiveAllInGroup message)
        {
            logger.InfoFormat("Archiving of {0} started", message.GroupId);
            var result = Session.Advanced.DocumentStore.DatabaseCommands.UpdateByIndex(
                            new FailedMessages_ByGroup().IndexName, 
                            new IndexQuery
                            {
                                Query = string.Format(CultureInfo.InvariantCulture, "FailureGroupId:{0} AND Status:{1}", message.GroupId, (int)FailedMessageStatus.Unresolved), 
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
                            }, new BulkOperationOptions { AllowStale = true }).WaitForCompletion();

            var patchedDocumentIds = result.JsonDeserialization<DocumentPatchResult[]>();
            logger.InfoFormat("Archiving of {0} ended", message.GroupId);
            logger.InfoFormat("Archived {0} for {1}", patchedDocumentIds.Length, message.GroupId);

            if (patchedDocumentIds.Length == 0)
            {
                return;
            }

            var failedMessage = Session.Load<FailedMessage>(patchedDocumentIds[0].Document);
            var failureGroup = failedMessage.FailureGroups.FirstOrDefault();
            var groupName = "Undefined";

            if (failureGroup != null && failureGroup.Title != null)
            {
                groupName = failureGroup.Title;
            }

            Bus.Publish<FailedMessageGroupArchived>(m =>
                {
                    m.GroupId = message.GroupId;
                    m.GroupName = groupName;
                    m.MessagesCount = patchedDocumentIds.Length;
                });
        }

        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        class DocumentPatchResult
        {
            public string Document { get; set; }
        }
    }
}
