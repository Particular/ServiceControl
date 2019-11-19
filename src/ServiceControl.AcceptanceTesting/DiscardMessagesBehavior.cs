namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;

    public class DiscardMessagesBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        public DiscardMessagesBehavior(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            //Do not filter out CC and HB messages as they can't be stamped
            if (context.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                && pluginMessages.Any(t => messageTypes.StartsWith(t)))
            {
                return next(context);
            }

            //Do not filter out performance metrics metrics
            if (context.Message.Headers.TryGetValue("NServiceBus.Metric.Type", out var metricType)
                && metricTypes.Any(t => metricType.StartsWith(t)))
            {
                return next(context);
            }

            //Do not filter out subscribe messages as they can't be stamped
            if (context.Message.Headers.TryGetValue(Headers.MessageIntent, out var intent)
                && intent == MessageIntentEnum.Subscribe.ToString())
            {
                return next(context);
            }

            var currentSession = scenarioContext.TestRunId.ToString();
            if (!context.Message.Headers.TryGetValue("SC.SessionID", out var session)
                || session != currentSession)
            {
                context.Message.Headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                context.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var enclosedMessageTypes);
                log.Debug($"Discarding message '{context.Message.MessageId}'({originalMessageId ?? string.Empty}) because it's session id is '{session}' instead of '{currentSession}' Message Types: {enclosedMessageTypes}.");
                return Task.FromResult(0);
            }

            return next(context);
        }

        ScenarioContext scenarioContext;
        static ILog log = LogManager.GetLogger<DiscardMessagesBehavior>();

        static string[] pluginMessages =
        {
            "ServiceControl.Plugin.CustomChecks.Messages.ReportCustomCheckResult",
            "ServiceControl.EndpointPlugin.Messages.SagaState.SagaChangeInitiator",
            "ServiceControl.EndpointPlugin.Messages.SagaState.SagaUpdatedMessage",
            "NServiceBus.Metrics.EndpointMetadataReport",
            "ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat",
            "ServiceControl.Plugin.Heartbeat.Messages.RegisterEndpointStartup"
        };

        static string[] metricTypes =
        {
            "ProcessingTime",
            "Retries",
            "QueueLength"
        };
    }
}