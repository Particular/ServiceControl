namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class FailureGroupsArchiveApi : BaseModule
    {
        public FailureGroupsArchiveApi()
        {
            Post["/recoverability/groups/{groupId}/errors/archive", true] =
                (parameters, ctx) => ArchiveGroupErrors(parameters.GroupId);
        }

        async Task<dynamic> ArchiveGroupErrors(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            if (!ArchiveOperationManager.IsOperationInProgressFor(groupId, ArchiveType.FailureGroup))
            {
                await ArchiveOperationManager.StartArchiving(groupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                await Bus.SendLocal<ArchiveAllInGroup>(m =>
                {
                    m.GroupId = groupId;
                }).ConfigureAwait(false);
            }

            return HttpStatusCode.Accepted;
        }

        public ArchivingManager ArchiveOperationManager { get; set; }
        public IEndpointInstance Bus { get; set; }
    }
}