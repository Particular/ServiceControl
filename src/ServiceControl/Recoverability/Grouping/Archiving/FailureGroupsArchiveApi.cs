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

            Bus.SendLocal<ArchiveAllInGroup>(m => m.GroupId = groupId);

            return HttpStatusCode.Accepted;
        }

        public IBus Bus { get; set; }
    }
}