namespace ServiceControl.CustomChecks
{
    using System;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    class ReportCustomCheckResultHandler : IHandleMessages<ReportCustomCheckResult>
    {
        IBus bus;
        IDocumentStore store;
        IDomainEvents domainEvents;

        public ReportCustomCheckResultHandler(IBus bus, IDocumentStore store, IDomainEvents domainEvents)
        {
            this.bus = bus;
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

            var publish = false;
            var id = DeterministicGuid.MakeId(message.EndpointName, message.HostId.ToString(), message.CustomCheckId);
            CustomCheck customCheck;

            using (var session = store.OpenSession())
            {
                customCheck = session.Load<CustomCheck>(id);

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
                session.Store(customCheck);
                session.SaveChanges();
            }

            if (publish)
            {
                if (message.HasFailed)
                {
                    domainEvents.Raise(new CustomCheckFailed
                    {
                        Id = id,
                        CustomCheckId = message.CustomCheckId,
                        Category = message.Category,
                        FailedAt = message.ReportedAt,
                        FailureReason = message.FailureReason,
                        OriginatingEndpoint = customCheck.OriginatingEndpoint
                    });
                }
                else
                {
                    domainEvents.Raise(new CustomCheckSucceeded
                    {
                        Id = id,
                        CustomCheckId = message.CustomCheckId,
                        Category = message.Category,
                        SucceededAt = message.ReportedAt,
                        OriginatingEndpoint = customCheck.OriginatingEndpoint
                    });
                }
            }
        }
    }
}