namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using AcceptanceTesting.Customization;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceControl.Monitoring;

    /// <summary>
    /// Base class for all the NSB test that sets up our conventions
    /// </summary>
    [TestFixture]
    // ReSharper disable once PartialTypeWithSinglePart
    public abstract partial class NServiceBusAcceptanceTest
    {
        protected NServiceBusAcceptanceTest()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            ServicePointManager.MaxServicePoints = int.MaxValue;
            ServicePointManager.UseNagleAlgorithm = false; // Improvement for small tcp packets traffic, get buffered up to 1/2-second. If your storage communication is for small (less than ~1400 byte) payloads, this setting should help (especially when dealing with things like Azure Queues, which tend to have very small messages).
            ServicePointManager.Expect100Continue = false; // This ensures tcp ports are free up quicker by the OS, prevents starvation of ports
            ServicePointManager.SetTcpKeepAlive(true, 5000, 1000); // This is good for Azure because it reuses connections
        }

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
                TransportType = TransportIntegration.MonitoringSeamTypeName,
                EnableInstallers = true, 
                ErrorQueue = "error",
                HttpHostName = "localhost",
                HttpPort = "1234"
            };

            var transportCustomization = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.TransportCustomization");

            if (transportCustomization != null && transportCustomization != typeof(ConfigureEndpointLearningTransport).Name)
            {
                Assert.Inconclusive($"Only running Monitoring ATT's for th learning transport, therefore skipping this test with '{TransportIntegration.MonitoringSeamTypeName}'");
            }
        }

        public static ITransportIntegration TransportIntegration { get; set; }

        public static Settings Settings { get; set; }

        public static string ConnectionString { get; set; }
    }
}