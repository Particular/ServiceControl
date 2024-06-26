﻿namespace Particular.LicensingComponent.UnitTests.Infrastructure
{
    using System;
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
            configuration = new ThroughputTestsConfiguration();

            return configuration.Configure(SetThroughputSettings, SetExtraDependencies);
        }

        [TearDown]
        public virtual Task Cleanup()
        {
            return configuration?.Cleanup();
        }

        protected ILicensingDataStore DataStore => configuration.LicensingDataStore;

        protected IThroughputCollector ThroughputCollector => configuration.ThroughputCollector;

        protected ThroughputTestsConfiguration configuration;
    }
}