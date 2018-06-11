namespace ServiceControl.CustomChecks
{
    using System;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    class ReportCustomCheckResultHandler : IHandleMessages<ReportCustomCheckResult>
    {
        IDocumentStore store;
        IDomainEvents domainEvents;

        public ReportCustomCheckResultHandler(IDocumentStore store, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
        }

        public void Handle(ReportCustomCheckResult message)
        {
            if (string.IsNullOrEmpty(message.EndpointName))
            {
                throw new Exception("Received an custom check message without proper initialization of the EndpointName in the schema");
            }

            if (string.IsNullOrEmpty(message.Host))
            {
                throw new Exception("Received an custom check message without proper initialization of the Host in the schema");
            }

            if (message.HostId == Guid.Empty)
            {
                throw new Exception("Received an custom check message without proper initialization of the HostId in the schema");
            }

            HandleAsync(message).GetAwaiter().GetResult();
        }

        private async Task HandleAsync(ReportCustomCheckResult message)
        {
            var publish = false;
            var id = DeterministicGuid.MakeId(message.EndpointName, message.HostId.ToString(), message.CustomCheckId);
            CustomCheck customCheck;

            using (var session = store.OpenAsyncSession())
            {
                customCheck = await session.LoadAsync<CustomCheck>(id)
                    .ConfigureAwait(false);

                if (customCheck == null ||
                    customCheck.Status == Status.Fail && !message.HasFailed ||
                    customCheck.Status == Status.Pass && message.HasFailed)
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

                customCheck.CustomCheckId = message.CustomCheckId;
                customCheck.Category = message.Category;
                customCheck.Status = message.HasFailed ? Status.Fail : Status.Pass;
                customCheck.ReportedAt = message.ReportedAt;
                customCheck.FailureReason = message.FailureReason;
                customCheck.OriginatingEndpoint = new EndpointDetails
                {
                    Host = message.Host,
                    HostId = message.HostId,
                    Name = message.EndpointName
                };
                await session.StoreAsync(customCheck)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            if (publish)
            {
                if (message.HasFailed)
                {
                    await domainEvents.Raise(new CustomCheckFailed
                    {
                        Id = id,
                        CustomCheckId = message.CustomCheckId,
                        Category = message.Category,
                        FailedAt = message.ReportedAt,
                        FailureReason = message.FailureReason,
                        OriginatingEndpoint = customCheck.OriginatingEndpoint
                    }).ConfigureAwait(false);
                }
                else
                {
                    await domainEvents.Raise(new CustomCheckSucceeded
                    {
                        Id = id,
                        CustomCheckId = message.CustomCheckId,
                        Category = message.Category,
                        SucceededAt = message.ReportedAt,
                        OriginatingEndpoint = customCheck.OriginatingEndpoint
                    }).ConfigureAwait(false);
                }
            }
        }
    }
}