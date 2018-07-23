namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Settings;

    class TraceOutgoingBehavior : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
    {
        public TraceOutgoingBehavior(ScenarioContext scenarioContext, ReadOnlySettings settings)
        {
            this.scenarioContext = scenarioContext;
            this.settings = settings;
        }

        public Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
        {
            scenarioContext.Logs.Enqueue(new ScenarioContext.LogItem
            {
                Endpoint = settings.EndpointName(),
                Level = LogLevel.Info,
                LoggerName = "Trace",
                Message = $"-> {context.Headers[Headers.MessageIntent]} {context.Message.MessageType.Name} ({context.Headers[Headers.MessageId].Substring(context.Headers[Headers.MessageId].Length - 4)})"
            });
            return next(context);
        }

        ScenarioContext scenarioContext;
        ReadOnlySettings settings;

        public class Registration : RegisterStep
        {
            public Registration()
                : base("TraceOutgoingBehavior", typeof(TraceOutgoingBehavior), "Adds outgoing messages to the acceptance test trace")
            {
            }
        }
    }
}