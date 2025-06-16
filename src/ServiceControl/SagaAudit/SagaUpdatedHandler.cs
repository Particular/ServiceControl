namespace ServiceControl.SagaAudit
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using EndpointPlugin.Messages.SagaState;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.Connection;
    using ServiceControl.Infrastructure;

    class SagaUpdatedHandler(IPlatformConnectionBuilder connectionBuilder, ILogger<SagaUpdatedHandler> logger)
        : IHandleMessages<SagaUpdatedMessage>
    {
        public async Task Handle(SagaUpdatedMessage message, IMessageHandlerContext context)
        {
            SagaAuditMisconfigurationCustomCheck.LogMisconfiguredMessage(context);

            if (auditQueueName is null || nextAuditQueueNameRefresh < DateTime.UtcNow)
            {
                await RefreshAuditQueue();
            }

            if (auditQueueName is null)
            {
                throw new UnrecoverableException("Could not determine audit queue name to forward saga update message. This message can be replayed after the ServiceControl Audit remote instance is running and accessible.");
            }

            var endpointName = context.MessageHeaders.TryGetValue(Headers.ReplyToAddress, out var val) ? val : "(Unknown Endpoint)";
            logger.LogError("Received a saga audit message in the ServiceControl queue that should have been sent to the audit queue. " +
                "This indicates that the endpoint '{endpointName}' using the SagaAudit plugin is misconfigured and should be changed to use the system's audit queue instead. " +
                "The message has been forwarded to the audit queue, but this may not be possible in a future version of ServiceControl.", endpointName);
            await context.ForwardCurrentMessageTo(auditQueueName);
        }

        async Task RefreshAuditQueue()
        {
            if (nextAuditQueueNameRefresh > DateTime.UtcNow)
            {
                return;
            }

            await semaphore.WaitAsync();
            try
            {
                if (nextAuditQueueNameRefresh > DateTime.UtcNow)
                {
                    return;
                }

                var connectionDetails = await connectionBuilder.BuildPlatformConnection();

                // First instance is named `SagaAudit`, following instance `SagaAudit1`..`SagaAuditN`
                if (connectionDetails.ToDictionary().TryGetValue("SagaAudit", out var sagaAuditObj) && sagaAuditObj is JsonElement sagaAudit)
                {
                    // Pick any audit queue, assume all instance are based on competing consumer
                    auditQueueName = sagaAudit.GetProperty("SagaAuditQueue").GetString();
                    nextAuditQueueNameRefresh = DateTime.UtcNow.AddMinutes(5);
                    logger.LogInformation("Refreshed audit queue name '{auditQueueName}' from ServiceControl Audit instance. Will continue to use this value for forwarding saga update messages for the next 5 minutes.", auditQueueName);
                }
            }
            catch (Exception x)
            {
                logger.LogWarning("Unable to refresh audit queue name from ServiceControl Audit instance. Will continue to check at most every 15 seconds. Exception message: {exceptionMessage}", x.Message);
                nextAuditQueueNameRefresh = DateTime.UtcNow.AddSeconds(15);
            }
            finally
            {
                semaphore.Release();
            }
        }

        static string auditQueueName;
        static DateTime nextAuditQueueNameRefresh;
        static readonly SemaphoreSlim semaphore = new(1);
        readonly ILogger<SagaUpdatedHandler> logger = logger;
    }
}
