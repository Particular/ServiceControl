namespace ServiceControl.Recoverability
{
    using System.Globalization;
    using System.Linq;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.MessageFailures;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public void Handle(ArchiveAllInGroup message)
        {
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
                            }, true).WaitForCompletion();

            var patchedDocumentIds = result.JsonDeserialization<DocumentPatchResult[]>()
                .Select(x => x.Document)
                .ToArray();

            if (patchedDocumentIds.Length == 0)
            {
                return;
            }

            var group = Session.Query<FailureGroupView, FailureGroupsViewIndex>()
                   .FirstOrDefault(x => x.Id == message.GroupId);

            var groupName = "Undefined";
            if (group != null && group.Title != null)
            {
                groupName = group.Title;
            }

            Bus.Publish<FailedMessageGroupArchived>(m =>
            {
                m.GroupId = message.GroupId;
                m.GroupName = groupName;
                m.MessageIds = patchedDocumentIds;
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
