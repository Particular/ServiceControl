namespace ServiceControl.AcceptanceTests.TestSupport
{
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public static class EndpointConfigurationExtensions
    {
        public static void ReportSuccessfulRetriesToServiceControl(this EndpointConfiguration configuration)
        {
            configuration.Pipeline.Register(typeof(ReportSuccessfulRetryToServiceControl), "Simulate that the audit instance detects and reports successfull retries");
        }

        public static void CustomizeServiceControlEndpointTesting(this EndpointConfiguration configuration, ScenarioContext context)
        {
            configuration.GetSettings().Set("SC.ScenarioContext", context);
            configuration.RegisterScenarioContext(context);

            configuration.Pipeline.Register<TraceIncomingBehavior.Registration>();
            configuration.Pipeline.Register<TraceOutgoingBehavior.Registration>();
            configuration.Pipeline.Register(new StampDispatchBehavior(context), "Stamps outgoing messages with session ID");
            configuration.Pipeline.Register(new DiscardMessagesBehavior(context), "Discards messages based on session ID");

            configuration.AssemblyScanner().Disable = true;
        }
    }
}