namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Linq;
    using System.Threading;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Logging;
    using NUnit.Framework;
    using NUnit.Framework.Internal;

    /// <summary>
    /// Base class for all the NSB test that sets up our conventions
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public abstract partial class NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.Use<DefaultFactory>(); // Ensures that every test the log manager is 'reset' as log manager can otherwise point to disposed resources. For example, when a test uses NServiceBus hosting

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
        }

        [TearDown]
        public void TearDown()
        {
            if (!TestExecutionContext.CurrentContext.TryGetRunDescriptor(out var runDescriptor))
            {
                return;
            }

            var scenarioContext = runDescriptor.ScenarioContext;

            TestContext.Out.WriteLine($@"Test settings:
{string.Join(Environment.NewLine, runDescriptor.Settings.Select(setting => $"   {setting.Key}: {setting.Value}"))}");

            TestContext.Out.WriteLine($@"Context:
{string.Join(Environment.NewLine, scenarioContext.GetType().GetProperties().Select(p => $"{p.Name} = {p.GetValue(scenarioContext, null)}"))}");
        }
    }
}