namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Settings;

    internal class TraceIncomingBehavior : IBehavior<IncomingContext>
    {
        private ScenarioContext scenarioContext;
        private ReadOnlySettings settings;

        public TraceIncomingBehavior(ScenarioContext scenarioContext, ReadOnlySettings settings)
        {
            this.scenarioContext = scenarioContext;
            this.settings = settings;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            scenarioContext.AddTrace($"<- {settings.LocalAddress()} got [{context.PhysicalMessage.Id}] {context.IncomingLogicalMessage.MessageType.Name}");
            next();
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("TraceIncomingBehavior", typeof(TraceIncomingBehavior), "Adds incoming messages to the acceptance test trace")
            {
                InsertBefore(WellKnownStep.LoadHandlers);
            }
        }
    }
}