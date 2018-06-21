namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Settings;

    internal class TraceIncomingBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        private ScenarioContext scenarioContext;
        private ReadOnlySettings settings;

        public TraceIncomingBehavior(ScenarioContext scenarioContext, ReadOnlySettings settings)
        {
            this.scenarioContext = scenarioContext;
            this.settings = settings;
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("TraceIncomingBehavior", typeof(TraceIncomingBehavior), "Adds incoming messages to the acceptance test trace")
            {
            }
        }

        public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            scenarioContext.Logs.Enqueue(new ScenarioContext.LogItem
            {
                Endpoint = settings.EndpointName(),
                Level = LogLevel.Info,
                LoggerName = "Trace",
                Message = $"<- {context.Message.MessageType.Name} ({context.Headers[Headers.MessageId].Substring(context.Headers[Headers.MessageId].Length - 4)})"
            });
            return next(context);
        }
    }
}