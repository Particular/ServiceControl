namespace ServiceControl.AcceptanceTests.TestSupport
{
    using ServiceBus.Management.AcceptanceTests;
    using NServiceBus;

    public static class EndpointConfigurationExtensions
    {
        public static void ReportSuccessfulRetriesToServiceControl(this EndpointConfiguration configuration)
        {
            configuration.Pipeline.Register(typeof(ReportSuccessfulRetryToServiceControl), "Simulate that the audit instance detects and reports successfull retries");
        }
    }
}