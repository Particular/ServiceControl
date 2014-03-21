namespace ServiceControl.CustomChecks
{
    using System;
    using System.Collections.Concurrent;
    using Contracts.CustomChecks;
    using Contracts.Operations;
    using Infrastructure;
    using NServiceBus;
    using Plugin.CustomChecks.Messages;
    using Raven.Client;

    class ReportCustomCheckResultHandler : IHandleMessages<ReportCustomCheckResult>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }

        public void Handle(ReportCustomCheckResult message)
        {
            if (string.IsNullOrEmpty(message.EndpointName))
            {
                throw new ArgumentException("Received an custom check message without proper initialization of the EndpointName in the schema", "message.EndpointName");
            }

            if (string.IsNullOrEmpty(message.Host))
            {
                throw new ArgumentException("Received an custom check message without proper initialization of the Host in the schema", "message.Host");
            }

            if (message.HostId == Guid.Empty)
            {
                throw new ArgumentException("Received an custom check message without proper initialization of the HostId in the schema", "message.HostId");
            }
                

            var id = DeterministicGuid.MakeId(message.EndpointName, message.HostId.ToString(), message.CustomCheckId);

            var customCheck = Session.Load<CustomCheck>(id) ?? new CustomCheck
            {
                Id = id,
            };

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

            DetectChanges(message, id, customCheck.OriginatingEndpoint);

            Session.Store(customCheck);
        }

        void DetectChanges(ReportCustomCheckResult message, Guid id,EndpointDetails endpoint)
        {
           var publish = true;

            registeredCustomChecks.AddOrUpdate(id,
                k => message,
                (k, existingValue) =>
                {
                    if (existingValue.HasFailed == message.HasFailed)
                    {
                        publish = false;
                        return existingValue;
                    }

                    return message;
                });

            if (!publish)
            {
                return;
            }

            if (message.HasFailed)
            {
                Bus.Publish<CustomCheckFailed>(m =>
                {
                    m.Id = id;
                    m.CustomCheckId = message.CustomCheckId;
                    m.Category = message.Category;
                    m.FailedAt = message.ReportedAt;
                    m.FailureReason = message.FailureReason;
                    m.OriginatingEndpoint = endpoint;
                });
            }
            else
            {
                Bus.Publish<CustomCheckSucceeded>(m =>
                {
                    m.Id = id;
                    m.CustomCheckId = message.CustomCheckId;
                    m.Category = message.Category;
                    m.SucceededAt = message.ReportedAt;
                    m.OriginatingEndpoint = endpoint;
                });
            }
        }

        readonly ConcurrentDictionary<Guid, ReportCustomCheckResult> registeredCustomChecks =
          new ConcurrentDictionary<Guid, ReportCustomCheckResult>();
    }
}