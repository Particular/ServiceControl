namespace ServiceControl.Recoverability
{
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class UnacknowledgedGroupsApi : BaseModule
    {
        public UnacknowledgedGroupsApi()
        {
            Delete["/recoverability/unacknowledgedgroups/{groupId}"] = parameters => AcknowledgeOperation(parameters);
        }

        private dynamic AcknowledgeOperation(dynamic parameters)
        {
            var groupId = parameters.groupId;

            ArchiveOperationManager.DismissArchiveOperation(groupId, ArchiveType.FailureGroup);

            return HttpStatusCode.OK;
        }
		public ArchivingManager ArchiveOperationManager { get; set; }
    }
}