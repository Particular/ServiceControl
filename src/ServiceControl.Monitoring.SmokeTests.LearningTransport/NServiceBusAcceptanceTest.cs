namespace ServiceControl.Monitoring.SmokeTests.LearningTransport
{
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using NUnit.Framework;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

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

            Settings = new Settings
            {
                TransportType = typeof(ServiceControlLearningTransport).AssemblyQualifiedName,
                EnableInstallers = true,
                ErrorQueue = "error",
                HttpHostName = "localhost",
                HttpPort = "1234"
            };
        }

        public static Settings Settings { get; set; }
    }
}