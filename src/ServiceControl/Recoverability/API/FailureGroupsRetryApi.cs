namespace ServiceControl.Recoverability
{
    using System;
    using Nancy;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class FailureGroupsRetryApi : BaseModule
    {
        public FailureGroupsRetryApi()
        {
            Post["/recoverability/groups/{groupId}/errors/retry"] =
                parameters => RetryAllGroupErrors(parameters.GroupId);
        }

        dynamic RetryAllGroupErrors(string groupId)
        {
            if (String.IsNullOrWhiteSpace(groupId))
            {
                return HttpStatusCode.BadRequest;
            }

            var started = DateTime.UtcNow;

            if (!RetryOperationManager.IsOperationInProgressFor(groupId, RetryType.FailureGroup))
            {
                RetryOperationManager.Wait(groupId, RetryType.FailureGroup, started);

                Bus.SendLocal(new RetryAllInGroup { GroupId = groupId, Started = started });
            }

            return HttpStatusCode.Accepted;
        }

        public IBus Bus { get; set; }
        public RetryingManager RetryOperationManager { get; set; }
    }
}