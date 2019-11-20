namespace ServiceControl.AcceptanceTests.TestSupport
{
    using NServiceBus;

    public static class EndpointConfigurationExtensions
    {
        public static void ReportSuccessfulRetriesToServiceControl(this EndpointConfiguration configuration)
        {
            configuration.Pipeline.Register(typeof(ReportSuccessfulRetryToServiceControl), "Simulate that the audit instance detects and reports successfull retries");
        }
    }
}