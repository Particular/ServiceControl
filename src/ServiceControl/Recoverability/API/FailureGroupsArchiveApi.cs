namespace ServiceControl.Recoverability
{
    using System;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class FailureGroupsArchiveApi : BaseModule
    {
        public FailureGroupsArchiveApi()
        {
            Post["/recoverability/groups/{groupId}/errors/archive"] =
                parameters => ArchiveGroupErrors(parameters.GroupId);
        }

        dynamic ArchiveGroupErrors(string groupId)
        {
            if (String.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            if (!ArchiveOperationManager.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                ArchiveOperationManager.StartArchiving(groupId, ArchiveType.FailureGroup);

                Bus.SendLocal<ArchiveAllInGroup>(m =>
                {
                    m.GroupId = groupId;
                });
            }

            return HttpStatusCode.Accepted;
        }

        public ArchivingManager ArchiveOperationManager { get; set; }
        public IBus Bus { get; set; }
    }
}