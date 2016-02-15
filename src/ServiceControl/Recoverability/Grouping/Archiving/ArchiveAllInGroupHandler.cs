namespace ServiceControl.Recoverability
{
    using System.Globalization;
    using System.Linq;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public void Handle(ArchiveAllInGroup message)
        {
            var group = Session.Query<FailureGroupView, FailureGroupsViewIndex>()
                   .FirstOrDefault(x => x.Id == message.GroupId);

            var groupName = "group";
            if (group != null && group.Title != null)
            {
                groupName = group.Title;
            }

            RavenJToken result = Session.Advanced.DocumentStore.DatabaseCommands.UpdateByIndex(
                            new FailedMessages_ByGroup().IndexName, 
                            new IndexQuery
                            {
                                Query = string.Format(CultureInfo.InvariantCulture, "FailureGroupId:{0} AND Status:{1}", message.GroupId, (int)FailedMessageStatus.Unresolved)
                            },
                            new[]
                            {
                                new PatchRequest
                                {
                                    Type = PatchCommandType.Set,
                                    Name = "Status",
                                    Value = (int) FailedMessageStatus.Archived
                                }
                            }).WaitForCompletion();

            var patchedDocumentIds = result.JsonDeserialization<DocumentPatchResult[]>()
                .Select(x => x.Document)
                .ToArray();

            Bus.Publish<FailedMessageGroupArchived>(m =>
            {
                m.GroupId = message.GroupId;
                m.GroupName = groupName;
                m.MessageIds = patchedDocumentIds;
            });
        }

        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
    }


    public class DocumentPatchResult
    {
        public string Document { get; set; }
        //public Results Result { get; set; }
        //public string[] Debug { get; set; }

        //public class Results
        //{
        //    public string PatchResult { get; set; }
        //    public string Document { get; set; }
        //}
    }
}
