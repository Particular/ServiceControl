namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
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
            scenarioContext.AddTrace($"<- {settings.LocalAddress()} got [{context.MessageId}] {context.Message.MessageType.Name}");
            return next(context);
        }
    }
}