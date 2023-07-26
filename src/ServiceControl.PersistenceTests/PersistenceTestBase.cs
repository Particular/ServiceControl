﻿namespace ServiceControl.PersistenceTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;

    [TestFixtureSource(typeof(PersistenceTestCollection))]
    abstract class PersistenceTestBase
    {
        TestPersistence persistence;
        IServiceProvider serviceProvider;

        public PersistenceTestBase(TestPersistence persistence)
        {
            this.persistence = persistence;
        }

        [SetUp]
        public virtual async Task Setup()
        {
            var services = new ServiceCollection();
            await persistence.Configure(services);
            serviceProvider = services.BuildServiceProvider();
        }

        [TearDown]
        public async Task Cleanup()
        {
            await persistence.CleanupDB();
        }

        protected Task CompleteDBOperation() => persistence.CompleteDBOperation();

        protected T GetService<T>() => serviceProvider.GetRequiredService<T>();
    }
}
