namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Settings;

    public class TraceIncomingBehavior(IReadOnlySettings settings) : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            var scenarioContext = settings.Get<ScenarioContext>();
            scenarioContext.Logs.Enqueue(new ScenarioContext.LogItem
            {
                Endpoint = settings.EndpointName(),
                Level = LogLevel.Info,
                LoggerName = "Trace",
                Message = $"<- {context.Message.MessageType.Name} ({context.Headers[Headers.MessageId].Substring(context.Headers[Headers.MessageId].Length - 4)})"
            });
            return next(context);
        }

        public class Registration() : RegisterStep("TraceIncomingBehavior", typeof(TraceIncomingBehavior), "Adds incoming messages to the acceptance test trace");
    }
}