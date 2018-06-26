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
                (parameters, ctx) => RetryAllGroupErrors(parameters.GroupId);
        }

        async Task<dynamic> RetryAllGroupErrors(string groupId)
        {
            if (String.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            var started = DateTime.UtcNow;

            if (!RetryOperationManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
            {
                await RetryOperationManager.Wait(groupId, RetryType.FailureGroup, started)
                    .ConfigureAwait(false);

                await Bus.Value.SendLocal(new RetryAllInGroup { GroupId = groupId, Started = started }).ConfigureAwait(false);
            }

            return HttpStatusCode.Accepted;
        }

        public Lazy<IEndpointInstance> Bus { get; set; }
        public RetryingManager RetryOperationManager { get; set; }
    }
}