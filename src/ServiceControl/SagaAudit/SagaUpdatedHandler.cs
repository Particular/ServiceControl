﻿namespace ServiceControl.SagaAudit
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.Connection;
    using ServiceControl.Infrastructure;
    using ServiceControl.Persistence;

    class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public SagaUpdatedHandler(ISagaAuditDataStore sagaAuditStore, IPlatformConnectionBuilder connectionBuilder)
        {
            this.sagaAuditStore = sagaAuditStore;
            this.connectionBuilder = connectionBuilder;
        }

        public async Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
        {
            var sagaSnapshot = SagaSnapshotFactory.Create(message);

            var supportedByDataStore = await sagaAuditStore.StoreSnapshot(sagaSnapshot);

            if (!supportedByDataStore)
            {
                if (auditQueueName is null || nextAuditQueueNameRefresh < DateTime.UtcNow)
                {
                    await RefreshAuditQueue();
                }

                if (auditQueueName is null)
                {
                    throw new UnrecoverableException("Could not determine audit queue name to forward saga update message. This message can be replayed after the ServiceControl Audit remote instance is running and accessible.");
                }

                await context.ForwardCurrentMessageTo(auditQueueName);
            }
        }

        async Task RefreshAuditQueue()
        {
            await semaphore.WaitAsync();
            try
            {
                if (nextAuditQueueNameRefresh > DateTime.UtcNow)
                {
                    return;
                }

                var connectionDetails = await connectionBuilder.BuildPlatformConnection();

                if (connectionDetails.ToDictionary().TryGetValue("SagaAudit", out var sagaAuditObj) && sagaAuditObj is JObject sagaAudit)
                {
                    auditQueueName = sagaAudit["SagaAuditQueue"].Value<string>();
                    nextAuditQueueNameRefresh = DateTime.UtcNow.AddMinutes(5);
                    log.InfoFormat("Refreshed audit queue name '{0}' from ServiceControl Audit instance. Will continue to use this value for forwarding saga update messages for the next 5 minutes.", auditQueueName);
                }
            }
            catch (Exception x)
            {
                log.WarnFormat("Unable to refresh audit queue name from ServiceControl Audit instance. Will continue to check at most every 15 seconds. Exception message: {0}", x.Message);
                nextAuditQueueNameRefresh = DateTime.UtcNow.AddSeconds(15);
            }
            finally
            {
                semaphore.Release();
            }
        }

        readonly ISagaAuditDataStore sagaAuditStore;
        readonly IPlatformConnectionBuilder connectionBuilder;

        static string auditQueueName;
        static DateTime nextAuditQueueNameRefresh;
        static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        static readonly ILog log = LogManager.GetLogger<SagaUpdatedHandler>();
    }
}
