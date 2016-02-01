namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class FailureGroupsArchiveApi : BaseModule
    {
        public FailureGroupsArchiveApi()
        {
            Post["/recoverability/groups/{groupId}/errors/archive", true] =
                async (parameters, ct) => await ArchiveGroupErrors(parameters.GroupId);
        }

        async Task<dynamic> ArchiveGroupErrors(string groupId)
        {
            if (String.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            await BusSession.SendLocal<ArchiveAllInGroup>(m => m.GroupId = groupId);

            return HttpStatusCode.Accepted;
        }

        public IBusSession BusSession { get; set; }
    }
}