namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;
    using ServiceControl.Audit.Persistence.EFCore.Implementation.BodyStorage;
    using ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    [TestFixture]
    class OptionalBodyStorageRegistrationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void File_system_check_is_registered_once_only_when_configured(bool configured)
        {
            var services = new ServiceCollection();
            if (configured)
            {
                services.AddSingleton(new FileSystemBodyStorageSettings { StoragePath = "unused" });
            }

            EFPersistenceTestsConfiguration.AddCustomChecks(services, typeof(OptionalBodyStorageRegistrationTests));

            Assert.That(services.Count(descriptor => descriptor.ServiceType == typeof(ICustomCheck)
                && descriptor.ImplementationType == typeof(AuditFileSystemBodyStorageCustomCheck)), Is.EqualTo(configured ? 1 : 0));
        }

        [Test]
        public void Consumers_resolve_without_a_body_store()
        {
            var settings = new EFPersisterSettings { ConnectionString = "unused", AuditRetentionPeriod = TimeSpan.FromDays(1) };
            var services = new ServiceCollection();
            services.AddSingleton(settings);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IServiceScopeFactory>(_ => null!);
            services.AddSingleton<IAuditPartitionManager>(_ => null!);
            services.AddSingleton<IRetentionLock>(_ => null!);
            services.AddSingleton<IFullTextSearchDialect>(_ => null!);
            services.AddSingleton<AuditRetentionCustomCheck.State>();
            services.AddLogging();
            services.AddSingleton<IAuditIngestionUnitOfWorkFactory, AuditIngestionUnitOfWorkFactory>();
            services.AddSingleton<AuditDataStore>();
            services.AddSingleton<AuditRetention>();

            using var provider = services.BuildServiceProvider();

            Assert.DoesNotThrow(() =>
            {
                provider.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
                provider.GetRequiredService<AuditDataStore>();
                provider.GetRequiredService<AuditRetention>();
            });
        }
    }
}
