namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;
    using ServiceControl.Audit.Persistence.EFCore.Implementation.UnitOfWork;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    [TestFixture]
    class OptionalBodyStorageRegistrationTests
    {
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
