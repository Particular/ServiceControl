namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;

    internal class DiscardMessagesBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        static string[] pluginMessages = new[]
        {
            "ServiceControl.Plugin.CustomChecks.Messages.ReportCustomCheckResult",
            "ServiceControl.EndpointPlugin.Messages.SagaState.SagaChangeInitiator",
            "ServiceControl.EndpointPlugin.Messages.SagaState.SagaUpdatedMessage",
            "ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat",
            "ServiceControl.Plugin.Heartbeat.Messages.RegisterEndpointStartup"
        };
        private ScenarioContext scenarioContext;

        public DiscardMessagesBehavior(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            string session;
            string intent;
            string messageTypes;
            //Do not filter out CC and HB messages as they can't be stamped
            if (context.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out messageTypes)
                && pluginMessages.Any(t => messageTypes.StartsWith(t)))
            {
                return next(context);
            }

            //Do not filter out subscribe messages as they can't be stamped
            if (context.Message.Headers.TryGetValue(Headers.MessageIntent, out intent)
                && intent == MessageIntentEnum.Subscribe.ToString())
            {
                return next(context);
            }

            var currentSession = scenarioContext.TestRunId.ToString();
            if (!context.Message.Headers.TryGetValue("SC.SessionID", out session) 
                || session != currentSession)
            {
                context.Message.Headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                log.Debug($"Discarding message '{context.Message.MessageId}'({originalMessageId ?? string.Empty}) because it's session id is '{session}' instead of '{currentSession}'.");
                return Task.FromResult(0);
            }

            return next(context);
        }

        private static ILog log = LogManager.GetLogger<DiscardMessagesBehavior>();
    }
}