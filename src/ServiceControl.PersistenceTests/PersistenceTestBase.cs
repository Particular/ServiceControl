namespace ServiceControl.PersistenceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;

    [TestFixtureSource(typeof(PersistenceTestCollection))]
    abstract class PersistenceTestBase
    {
        TestPersistence persistence;
        IServiceProvider serviceProvider;
        IHostedService[] hostedServices;

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

            // TODO: Combine with logic in PersistenceTestBase
            hostedServices = serviceProvider.GetServices<IHostedService>().ToArray();
            foreach (var service in hostedServices)
            {
                await service.StartAsync(default);
            }
        }

        [TearDown]
        public async Task Cleanup()
        {
            await persistence.CleanupDB();

            foreach (var service in hostedServices.Reverse())
            {
                await service.StopAsync(default);
            }
        }

        protected Task CompleteDBOperation() => persistence.CompleteDBOperation();

        protected T GetService<T>() => serviceProvider.GetRequiredService<T>();
    }
}
