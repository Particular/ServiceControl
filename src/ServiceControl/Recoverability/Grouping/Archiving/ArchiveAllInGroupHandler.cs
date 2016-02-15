namespace ServiceControl.Recoverability
{
    using System.Globalization;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Json.Linq;
    using ServiceControl.MessageFailures;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        public void Handle(ArchiveAllInGroup message)
        {
            RavenJToken result = Session.Advanced.DocumentStore.DatabaseCommands.UpdateByIndex(
                            "FailedMessages/ByGroup",
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

            //TODO: What to do with result, it looks like:
            /*
            [
                {
                    "Document": "failedmessages/46aa0a73-fa28-81be-63b4-813895af1d6f",
                    "Result": {
                        "PatchResult": "Patched",
                        "Document": null
                    }
                },
                {
                    "Document": "failedmessages/40432c54-2ecd-dc35-8ec1-6996a0002913",
                    "Result": {
                        "PatchResult": "Patched",
                        "Document": null
                    }
                }
            ]
            */
            Bus.Publish<FailedMessageGroupArchived>(m =>
            {
                m.GroupId = message.GroupId;
                m.GroupName = message.GroupId;
                m.MessageIds = new []{ result.ToString() };
            });
        }

        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
    }
}
