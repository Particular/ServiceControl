namespace ServiceControl.SagaAudit
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using ServiceControl.Connection;
    using ServiceControl.Infrastructure.DomainEvents;

    class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public SagaUpdatedHandler(IDomainEvents domainEvents, IPlatformConnectionBuilder connectionBuilder)
        {
            this.domainEvents = domainEvents;
            this.connectionBuilder = connectionBuilder;
        }

        public async Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
        {
            await WarnOncePerEnpointPerDay(message);

            if (auditQueueName is null || nextAuditQueueNameRefresh < DateTime.UtcNow)
            {
                await RefreshAuditQueue();
            }

            if (auditQueueName is null)
            {
                throw new Exception("Could not determine audit queue name to forward saga update message. The ServiceControl remote audit instance ");
            }

            await context.ForwardCurrentMessageTo(auditQueueName);
        }

        async Task RefreshAuditQueue()
        {
            await semaphore.WaitAsync();
            try
            {
                if (auditQueueName != null && nextAuditQueueNameRefresh > DateTime.UtcNow)
                {
                    return;
                }

                var connectionDetails = await connectionBuilder.BuildPlatformConnection();

                if (connectionDetails.ToDictionary().TryGetValue("SagaAudit", out var sagaAuditObj) && sagaAuditObj is JObject sagaAudit)
                {
                    auditQueueName = sagaAudit["SagaAuditQueue"].Value<string>();
                    nextAuditQueueNameRefresh = DateTime.UtcNow.AddMinutes(5);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                semaphore.Release();
            }
        }

        async Task WarnOncePerEnpointPerDay(SagaUpdatedMessage message)
        {
            if (nextWarningDates.TryGetValue(message.Endpoint, out var nextWarning) && nextWarning > DateTime.UtcNow)
            {
                return;
            }

            await semaphore.WaitAsync();
            try
            {
                if (nextWarningDates.TryGetValue(message.Endpoint, out nextWarning) && nextWarning > DateTime.UtcNow)
                {
                    return;
                }

                await domainEvents.Raise(new EndpointReportingSagaAuditToPrimary
                {
                    DetectedAt = DateTime.UtcNow,
                    EndpointName = message.Endpoint
                });

                nextWarningDates[message.Endpoint] = DateTime.UtcNow.AddDays(1);
            }
            finally
            {
                semaphore.Release();
            }
        }

        readonly IDomainEvents domainEvents;
        readonly IPlatformConnectionBuilder connectionBuilder;

        static ConcurrentDictionary<string, DateTime> nextWarningDates = new ConcurrentDictionary<string, DateTime>();
        static string auditQueueName;
        static DateTime nextAuditQueueNameRefresh;
        static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
    }
}