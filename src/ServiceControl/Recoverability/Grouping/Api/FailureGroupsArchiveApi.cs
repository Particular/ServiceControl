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


            Delete["/recoverability/groups/unacknowledgedgroups/{groupId}"] =
                parameters => AcknowledgeOperation(parameters);
        }

        private dynamic AcknowledgeOperation(dynamic parameters)
        {
            var groupId = parameters.groupId;

            ArchiveOperationManager.DismissArchiveOperation(groupId, ArchiveType.FailureGroup);

            return HttpStatusCode.OK;
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
                    m.CutOff = DateTime.UtcNow;
                });
            }

            return HttpStatusCode.Accepted;
        }

		public OperationManager ArchiveOperationManager { get; set; }
        public IBus Bus { get; set; }
    }
}