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

            RetryOperationManager.Wait(groupId, RetryType.FailureGroup);

            Bus.SendLocal<RetryAllInGroup>(m => m.GroupId = groupId);

            return HttpStatusCode.Accepted;
        }

        public IBus Bus { get; set; }
        public RetryOperationManager RetryOperationManager { get; set; }
    }
}