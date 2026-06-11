namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Settings;

    public class TraceOutgoingBehavior(IReadOnlySettings settings) : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
    {
        public Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
        {
            var scenarioContext = settings.Get<ScenarioContext>();
            scenarioContext.Logs.Enqueue(new ScenarioContext.LogItem
            {
                Endpoint = settings.EndpointName(),
                Level = LogLevel.Info,
                LoggerName = "Trace",
                Message = $"-> {context.Headers[Headers.MessageIntent]} {context.Message.MessageType.Name} ({context.Headers[Headers.MessageId].Substring(context.Headers[Headers.MessageId].Length - 4)})"
            });
            return next(context);
        }

        public class Registration() : RegisterStep("TraceOutgoingBehavior", typeof(TraceOutgoingBehavior), "Adds outgoing messages to the acceptance test trace");
    }
}