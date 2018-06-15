namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Settings;

    internal class TraceOutgoingBehavior : IBehavior<IOutgoingLogicalMessageContext, IOutgoingLogicalMessageContext>
    {
        private ScenarioContext scenarioContext;
        private ReadOnlySettings settings;

        public TraceOutgoingBehavior(ScenarioContext scenarioContext, ReadOnlySettings settings)
        {
            this.scenarioContext = scenarioContext;
            this.settings = settings;
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("TraceOutgoingBehavior", typeof(TraceOutgoingBehavior), "Adds outgoing messages to the acceptance test trace")
            {
            }
        }

        public Task Invoke(IOutgoingLogicalMessageContext context, Func<IOutgoingLogicalMessageContext, Task> next)
        {
            scenarioContext.AddTrace($"-> {context.Headers[Headers.MessageIntent]} from {settings.LocalAddress()} [{context.MessageId}] {context.Message.MessageType.Name}");
            return next(context);
        }
    }
}