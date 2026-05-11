namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport;

using AcceptanceTesting;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.Configuration.AdvancedExtensibility;

static class EndpointConfigurationExtensions
{
    public static void CustomizeServiceControlMonitoringEndpointTesting(this EndpointConfiguration configuration, ScenarioContext context)
    {
        configuration.GetSettings().Set("SC.ScenarioContext", context);
        configuration.RegisterScenarioContext(context);

        configuration.Pipeline.Register<TraceIncomingBehavior.Registration>();
        configuration.Pipeline.Register<TraceOutgoingBehavior.Registration>();
        configuration.Pipeline.Register(new StampDispatchBehavior(context), "Stamps outgoing messages with session ID");
        configuration.Pipeline.Register(new DiscardMessagesBehavior(context), "Discards messages based on session ID");
    }
}