namespace ServiceControl.SagaAudit
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using ServiceControl.Connection;
    using ServiceControl.Infrastructure;

    class SagaUpdatedHandler : IHandleMessages<SagaUpdatedMessage>
    {
        public SagaUpdatedHandler(IPlatformConnectionBuilder connectionBuilder, SagaAuditDestinationCustomCheck.State customCheckState)
        {
            this.connectionBuilder = connectionBuilder;
            this.customCheckState = customCheckState;
        }

        public async Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
        {
            customCheckState.Fail(message.Endpoint);

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

        readonly IPlatformConnectionBuilder connectionBuilder;
        readonly SagaAuditDestinationCustomCheck.State customCheckState;

        static string auditQueueName;
        static DateTime nextAuditQueueNameRefresh;
        static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
    }
}
