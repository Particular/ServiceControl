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
            if (ArchiveOperationManager.IsArchiveInProgressFor(groupId))
            {
                ArchiveOperationManager.DismissArchiveOperation(groupId, ArchiveType.FailureGroup);
                return HttpStatusCode.OK;
            }

            using (var session = Store.OpenSession())
            {
                var retryHistory = session.Load<RetryHistory>(RetryHistory.MakeId());
                if (retryHistory != null)
                {
                    if (retryHistory.Acknowledge(groupId, RetryType.FailureGroup))
                    {
                        session.Store(retryHistory);
                        session.SaveChanges();

                        return HttpStatusCode.OK;
                    }
                }
            }

            return HttpStatusCode.NotFound;
        }

        public ArchivingManager ArchiveOperationManager { get; set; }
        public RetryingManager RetryingOperationManager { get; set; }
    }
}