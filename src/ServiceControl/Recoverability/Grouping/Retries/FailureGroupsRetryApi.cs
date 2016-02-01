namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class FailureGroupsRetryApi : BaseModule
    {
        public FailureGroupsRetryApi()
        {
            Post["/recoverability/groups/{groupId}/errors/retry", true] =
                async (parameters, ct) => await RetryAllGroupErrors(parameters.GroupId);
        }

        async Task<dynamic> RetryAllGroupErrors(string groupId)
        {
            if (String.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            await BusSession.SendLocal<RetryAllInGroup>(m => m.GroupId = groupId);

            return HttpStatusCode.Accepted;
        }

        public IBusSession BusSession { get; set; }
    }
}