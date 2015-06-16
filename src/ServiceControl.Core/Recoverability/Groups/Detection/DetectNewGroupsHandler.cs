namespace ServiceControl.Recoverability.Groups.Detection
{
    using System.Linq;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.InternalContracts.Messages.MessageFailures;
    using ServiceControl.Recoverability.Groups.Indexes;

    public class DetectNewGroupsHandler : IHandleMessages<DetectNewGroups>
    {
        public void Handle(DetectNewGroups message)
        {
            var toAnnounce = Session.Query<FailureGroup, FailureGroupsIndex>()
                .Customize(q => q.WaitForNonStaleResultsAsOf(message.EndOfWindow.UtcDateTime))
                .Where(f => f.First > message.StartOfWindow.UtcDateTime && f.First <= message.EndOfWindow.UtcDateTime)
                .OrderByDescending(f => f.Count)
                .ToArray();

            foreach (var failureGroup in toAnnounce)
            {
                Bus.Publish(new NewFailureGroupDetected
                {
                    GroupId = failureGroup.Id,
                    GroupName = failureGroup.Title
                });
            }
        }

        public IBus Bus { get; set; }
        public IDocumentSession Session { get; set; }
    }
}
