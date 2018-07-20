namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class UnacknowledgedGroupsApi : BaseModule
    {
        public UnacknowledgedGroupsApi()
        {
            Delete["/recoverability/unacknowledgedgroups/{groupId}", true] = (parameters, token) => AcknowledgeOperation(parameters);
        }

        async Task<dynamic> AcknowledgeOperation(dynamic parameters)
        {
            var groupId = parameters.groupId;
            if (ArchiveOperationManager.IsArchiveInProgressFor(groupId))
            {
                ArchiveOperationManager.DismissArchiveOperation(groupId, ArchiveType.FailureGroup);
                return HttpStatusCode.OK;
            }

            using (var session = Store.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false);
                if (retryHistory != null)
                {
                    if (retryHistory.Acknowledge(groupId, RetryType.FailureGroup))
                    {
                        await session.StoreAsync(retryHistory).ConfigureAwait(false);
                        await session.SaveChangesAsync().ConfigureAwait(false);

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