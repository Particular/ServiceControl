namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Infrastructure;
    using Infrastructure.DomainEvents;
    using Raven.Client;

    class CustomChecksStorage
    {
        public CustomChecksStorage(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public async Task UpdateCustomCheckStatus(EndpointDetails originatingEndpoint, DateTime reportedAt, string customCheckId, string category, bool hasFailed, string failureReason)
        {
            var publish = false;
            var id = DeterministicGuid.MakeId(originatingEndpoint.Name, originatingEndpoint.HostId.ToString(), customCheckId);

            using (var session = store.OpenAsyncSession())
            {
                var customCheck = await session.LoadAsync<CustomCheck>(id)
                    .ConfigureAwait(false);

                if (customCheck == null ||
                    (customCheck.Status == Status.Fail && !hasFailed) ||
                    (customCheck.Status == Status.Pass && hasFailed))
                {
                    if (customCheck == null)
                    {
                        customCheck = new CustomCheck
                        {
                            Id = id
                        };
                    }

                    publish = true;
                }

                customCheck.CustomCheckId = customCheckId;
                customCheck.Category = category;
                customCheck.Status = hasFailed ? Status.Fail : Status.Pass;
                customCheck.ReportedAt = reportedAt;
                customCheck.FailureReason = failureReason;
                customCheck.OriginatingEndpoint = originatingEndpoint;
                await session.StoreAsync(customCheck)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            if (publish)
            {
                if (hasFailed)
                {
                    await domainEvents.Raise(new CustomCheckFailed
                    {
                        Id = id,
                        CustomCheckId = customCheckId,
                        Category = category,
                        FailedAt = reportedAt,
                        FailureReason = failureReason,
                        OriginatingEndpoint = originatingEndpoint
                    }).ConfigureAwait(false);
                }
                else
                {
                    await domainEvents.Raise(new CustomCheckSucceeded
                    {
                        Id = id,
                        CustomCheckId = customCheckId,
                        Category = category,
                        SucceededAt = reportedAt,
                        OriginatingEndpoint = originatingEndpoint
                    }).ConfigureAwait(false);
                }
            }

        }

        IDocumentStore store;
        IDomainEvents domainEvents;
    }
}