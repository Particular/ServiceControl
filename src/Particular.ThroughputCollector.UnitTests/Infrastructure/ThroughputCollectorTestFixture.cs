namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Persistence;

    [TestFixture]
    abstract class ThroughputCollectorTestFixture
    {
        //public Action<PersistenceSettings> SetPersistenceSettings = _ => { };
        public Action<ThroughputSettings> SetThroughputSettings = _ => { };
        public List<Endpoint> EndpointsWithThroughput = [];

        [SetUp]
        public virtual Task Setup()
        {
            configuration = new ThroughputTestsConfiguration();

            return configuration.Configure(SetThroughputSettings, EndpointsWithThroughput);

            //return Task.CompletedTask;
        }

        [TearDown]
        public virtual Task Cleanup()
        {
            return configuration?.Cleanup();
        }

        protected IThroughputDataStore DataStore => configuration.ThroughputDataStore;

        protected IThroughputCollector ThroughputCollector => configuration.ThroughputCollector;

        protected ThroughputTestsConfiguration configuration;
    }
}