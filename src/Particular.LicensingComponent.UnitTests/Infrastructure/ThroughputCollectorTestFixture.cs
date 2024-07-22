namespace Particular.LicensingComponent.UnitTests.Infrastructure
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using Persistence;

    [TestFixture]
    abstract class ThroughputCollectorTestFixture
    {
        public Action<ThroughputSettings> SetThroughputSettings = _ => { };
        public Action<ServiceCollection> SetExtraDependencies = _ => { };

        [SetUp]
        public virtual Task Setup()
        {
            var testMethod = GetType().GetMethod(TestContext.CurrentContext.Test.MethodName);
            var attribute = testMethod?.GetCustomAttributes(typeof(UseNonBrokerTransportAttribute), false).FirstOrDefault();

            return configuration.Configure(SetThroughputSettings, SetExtraDependencies, attribute is not UseNonBrokerTransportAttribute);
        }

        [TearDown]
        public virtual Task Cleanup() => configuration?.Cleanup();

        protected ILicensingDataStore DataStore => configuration.LicensingDataStore;

        protected IThroughputCollector ThroughputCollector => configuration.ThroughputCollector;

        protected ThroughputTestsConfiguration configuration = new();

        [AttributeUsage(AttributeTargets.Method)]
        public class UseNonBrokerTransportAttribute : Attribute
        {
        }
    }
}