namespace ServiceControl.Audit.Persistence.Tests.MongoDB.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Persistence.MongoDB;
    using Infrastructure;

    /// <summary>
    /// Base class for FailedAuditStorage tests that can run against different MongoDB-compatible products.
    /// </summary>
    public abstract class FailedAuditStorageTestsBase
    {
        protected IMongoTestEnvironment Environment { get; private set; }

        IHost host;
        string databaseName;
        IFailedAuditStorage failedAuditStorage;

        protected abstract IMongoTestEnvironment CreateEnvironment();

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            Environment = CreateEnvironment();
            await Environment.Initialize().ConfigureAwait(false);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            if (Environment != null)
            {
                await Environment.Cleanup().ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUp()
        {
            databaseName = $"test_{Guid.NewGuid():N}";
            var connectionString = Environment.BuildConnectionString(databaseName);

            var persistenceSettings = new PersistenceSettings(TimeSpan.FromHours(1), true, 100000);
            persistenceSettings.PersisterSpecificSettings[MongoPersistenceConfiguration.ConnectionStringKey] = connectionString;

            var config = new MongoPersistenceConfiguration();
            var persistence = config.Create(persistenceSettings);

            var hostBuilder = Host.CreateApplicationBuilder();
            persistence.AddPersistence(hostBuilder.Services);

            host = hostBuilder.Build();
            await host.StartAsync().ConfigureAwait(false);

            failedAuditStorage = host.Services.GetRequiredService<IFailedAuditStorage>();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (host != null)
            {
                var clientProvider = host.Services.GetRequiredService<IMongoClientProvider>();
                await clientProvider.Database.Client.DropDatabaseAsync(databaseName).ConfigureAwait(false);
                await host.StopAsync().ConfigureAwait(false);
                host.Dispose();
            }
        }

        [Test]
        public async Task Should_save_failed_audit_import()
        {
            var failedImport = CreateFailedAuditImport("test-msg-1");

            await failedAuditStorage.SaveFailedAuditImport(failedImport).ConfigureAwait(false);

            var count = await failedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        static FailedAuditImport CreateFailedAuditImport(string messageId)
        {
            return new FailedAuditImport
            {
                Id = $"FailedAuditImports/{Guid.NewGuid()}",
                Message = new FailedTransportMessage
                {
                    Id = messageId,
                    Headers = new Dictionary<string, string>
                    {
                        ["NServiceBus.MessageId"] = messageId,
                        ["NServiceBus.EnclosedMessageTypes"] = "TestMessage"
                    },
                    Body = System.Text.Encoding.UTF8.GetBytes($"Body for {messageId}")
                },
                ExceptionInfo = $"Exception for {messageId}"
            };
        }
    }
}
