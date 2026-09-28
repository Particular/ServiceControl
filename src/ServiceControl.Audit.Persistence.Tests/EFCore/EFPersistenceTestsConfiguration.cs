namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Time.Testing;
    using NServiceBus.CustomChecks;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;
    using UnitOfWork;

    abstract class EFPersistenceTestsConfiguration : IPersistenceTestsConfiguration
    {
        public IAuditMessagesViewDataStore MessagesViewStore { get; private set; }

        public ISagaHistoryDataStore SagaHistoryStore { get; private set; }

        public IFailedAuditStorage FailedAuditStorage { get; private set; }

        public IBodyStorage BodyStorage { get; private set; }

        public IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; private set; }

        public IServiceProvider ServiceProvider => host.Services;

        public abstract string Name { get; }

        public string ConnectionString { get; private set; }

        public string Schema { get; } = $"sc_test_{Guid.NewGuid():N}";

        // PostgreSQL keeps only microseconds, so a finer seed would not round trip.
        public FakeTimeProvider TimeProvider { get; } = new(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        public async Task Configure(Action<PersistenceSettings> setSettings)
        {
            ConnectionString = await GetConnectionString();
            await CreateSchema(ConnectionString, Schema);

            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = ConnectionString;
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = Schema;

            setSettings(settings);

            var configuration = CreateConfiguration();
            var persistence = configuration.Create(settings);

            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Services.AddSingleton<TimeProvider>(TimeProvider);

            persistence.AddInstaller(hostBuilder.Services);
            persistence.AddPersistence(hostBuilder.Services);

            // Tests sweep themselves, which the hourly loop would race whenever a test moves the clock.
            foreach (var retention in hostBuilder.Services.Where(descriptor => descriptor.ImplementationType == typeof(AuditRetention)).ToList())
            {
                hostBuilder.Services.Remove(retention);
            }

            foreach (var type in configuration.GetType().Assembly.DefinedTypes.Concat(typeof(EFPersistenceConfigurationBase).Assembly.DefinedTypes))
            {
                if (!type.IsAbstract && type.IsAssignableTo(typeof(ICustomCheck)))
                {
                    hostBuilder.Services.AddTransient(typeof(ICustomCheck), type);
                }
            }

            host = hostBuilder.Build();
            await host.StartAsync();

            MessagesViewStore = DecorateMessagesViewStore(host.Services.GetRequiredService<IAuditMessagesViewDataStore>());
            SagaHistoryStore = host.Services.GetRequiredService<ISagaHistoryDataStore>();
            FailedAuditStorage = host.Services.GetRequiredService<IFailedAuditStorage>();
            AuditIngestionUnitOfWorkFactory = host.Services.GetRequiredService<IAuditIngestionUnitOfWorkFactory>();
            BodyStorage = new MessagesViewBodyStorage(MessagesViewStore);
        }

        public virtual Task CompleteDBOperation() => Task.CompletedTask;

        protected virtual IAuditMessagesViewDataStore DecorateMessagesViewStore(IAuditMessagesViewDataStore store) => store;

        public async Task Cleanup()
        {
            if (host != null)
            {
                await host.StopAsync();
                host.Dispose();
            }

            if (ConnectionString != null)
            {
                await DropSchema(ConnectionString, Schema);
            }
        }

        internal AuditRetention CreateRetention() => ActivatorUtilities.CreateInstance<AuditRetention>(ServiceProvider);

        public abstract Task<string> GetConnectionString();

        protected abstract Task CreateSchema(string connectionString, string schema);

        protected abstract Task DropSchema(string connectionString, string schema);

        public abstract EFPersistenceConfigurationBase CreateConfiguration();

        IHost host;

        sealed class MessagesViewBodyStorage(IAuditMessagesViewDataStore messagesViewStore) : IBodyStorage
        {
            public Task Store(string bodyId, string contentType, int bodySize, System.IO.Stream bodyStream, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The EF Core persisters store a body with the message it belongs to.");

            public Task<MessageBodyView> TryFetch(string bodyId, CancellationToken cancellationToken = default) =>
                messagesViewStore.GetMessageBody(bodyId, cancellationToken);
        }
    }
}
