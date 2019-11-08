namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using AcceptanceTesting.Customization;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceControl.Monitoring;

    /// <summary>
    /// Base class for all the NSB test that sets up our conventions
    /// </summary>
    [TestFixture]
    public class NServiceBusAcceptanceTest2
    {
        public static ITransportIntegration TransportIntegration { get; set; }

        public static Settings Settings { get; set; }

        public static string ConnectionString { get; set; }

        [SetUp]
        public void SetUp()
        {
            Conventions.EndpointNamingConvention = t =>
            {
                var classAndEndpoint = t.FullName.Split('.').Last();

                var testName = classAndEndpoint.Split('+').First();

                testName = testName.Replace("When_", "");

                var endpointBuilder = classAndEndpoint.Split('+').Last();

                testName = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName);

                testName = testName.Replace("_", "");

                return testName + "." + endpointBuilder;
            };

            TransportIntegration = new ConfigureEndpointLearningTransport();

            ConnectionString = TransportIntegration.ConnectionString;

            Settings = new Settings
            {
                TransportType = TransportIntegration.TypeName,
                EnableInstallers = true,
                ErrorQueue = "error",
                HttpHostName = "localhost",
                HttpPort = "1234"
            };

            var transportCustomization = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.TransportCustomization");

            if (transportCustomization != null && transportCustomization != typeof(ConfigureEndpointLearningTransport).Name)
            {
                Assert.Inconclusive($"Only running Monitoring ATT's for the learning transport, therefore skipping this test with '{TransportIntegration.TypeName}'");
            }
        }
    }
}