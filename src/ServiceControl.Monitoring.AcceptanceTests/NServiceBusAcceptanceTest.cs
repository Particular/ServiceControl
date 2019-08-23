namespace NServiceBus.AcceptanceTests
{
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
    // ReSharper disable once PartialTypeWithSinglePart
    public abstract partial class NServiceBusAcceptanceTest
    {
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

            TransportIntegration = TestSuiteConstraints.CreateTransportConfiguration();

            ConnectionString = TransportIntegration.ConnectionString;

            Settings = new Settings
            {
                TransportType = TransportIntegration.MonitoringSeamTypeName,
                EnableInstallers = true, 
                ErrorQueue = "error",
                HttpHostName = "localhost",
                HttpPort = "1234"
            };
        }

        public static ITransportIntegration TransportIntegration { get; set; }

        public static Settings Settings { get; set; }

        public static string ConnectionString { get; set; }
    }
}