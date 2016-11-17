namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Settings;

    internal class TraceOutgoingBehavior : IBehavior<OutgoingContext>
    {
        private ScenarioContext scenarioContext;
        private ReadOnlySettings settings;

        public TraceOutgoingBehavior(ScenarioContext scenarioContext, ReadOnlySettings settings)
        {
            this.scenarioContext = scenarioContext;
            this.settings = settings;
        }

        public void Invoke(OutgoingContext context, Action next)
        {
            scenarioContext.AddTrace($"-> {context.OutgoingMessage.MessageIntent} from {settings.LocalAddress()} [{context.OutgoingMessage.Id}] {context.OutgoingLogicalMessage.MessageType.Name}");
            next();
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("TraceOutgoingBehavior", typeof(TraceOutgoingBehavior), "Adds outgoing messages to the acceptance test trace")
            {
                InsertBefore(WellKnownStep.DispatchMessageToTransport);
            }
        }
    }
}