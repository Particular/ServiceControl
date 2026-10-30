namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Npgsql;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.Entities;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;
    using ServiceControl.Persistence.Tests;
    using ServiceControl.PostgreSql.TokenAuthentication;

    [TestFixture]
    class TokenAuthenticationTests
    {
        [TestCase("Token Authentication=AzureEntraId;Token Scope=https://ossrdbms-aad.database.windows.net/.default")]
        [TestCase("Token Authentication=AwsRdsIam")]
        public async Task Migrates_and_queries_with_one_token_for_many_connections(string keywords)
        {
            var (connectionString, password) = await TokenConnectionStrings.ForSharedContainer(keywords);
            var schema = $"sc_token_{Guid.NewGuid():n}";
            var tokenProvider = new CountingTokenProvider(password);

            try
            {
                await using var provider = BuildServiceProvider(connectionString, schema, tokenProvider);

                await ApplyMigrations(provider);
                await RoundTrip(provider);
                await QueryOnConcurrentConnections(provider, connections: 3);
                var hosting = await provider.GetRequiredService<IDatabaseHostingProbe>().Probe();
                var identity = await provider.GetRequiredService<IStorageIdentityProvider>().GetIdentity();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(hosting.Source, Is.EqualTo(DatabaseHostingSource.Probe));
                    Assert.That(identity?.Schema, Is.EqualTo(schema));
                    Assert.That(tokenProvider.Calls, Is.EqualTo(1));
                    Assert.That(tokenProvider.Username, Is.EqualTo(new NpgsqlConnectionStringBuilder(provider.GetRequiredService<EFPersisterSettings>().ConnectionString).Username));
                }
            }
            finally
            {
                await TestSchema.Drop(await PostgreSqlSharedContainer.GetConnectionStringAsync(), schema);
            }
        }

        [Test]
        public async Task Acquires_and_releases_the_retention_lock_on_unpooled_token_connections()
        {
            var (connectionString, password) = await TokenConnectionStrings.ForSharedContainer("Token Authentication=AwsRdsIam");
            var tokenProvider = new CountingTokenProvider(password);
            await using var provider = BuildServiceProvider(connectionString, $"sc_token_{Guid.NewGuid():n}", tokenProvider);
            var retentionLock = provider.GetRequiredService<IRetentionLock>();

            var first = await retentionLock.TryAcquire();
            var whileHeld = await retentionLock.TryAcquire();
            await first!.DisposeAsync();
            var afterRelease = await retentionLock.TryAcquire();
            await afterRelease!.DisposeAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(whileHeld, Is.Null);
                Assert.That(afterRelease, Is.Not.Null);
                Assert.That(new NpgsqlConnectionStringBuilder(provider.GetRequiredKeyedService<NpgsqlDataSource>(PostgreSqlRetentionLock.TokenDataSourceKey).ConnectionString).Pooling, Is.False);
                Assert.That(tokenProvider.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public void Registers_the_data_sources_only_with_token_authentication()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(DataSourceKeys("Host=localhost;Username=sc"), Is.Empty);
                Assert.That(DataSourceKeys("Host=localhost;Username=sc;Token Authentication=AwsRdsIam"), Is.EquivalentTo(new object[] { null, PostgreSqlRetentionLock.TokenDataSourceKey }));
            }
        }

        [Test]
        public async Task Disposes_the_data_sources_with_the_container()
        {
            var provider = BuildServiceProvider("Host=localhost;Username=sc;Token Authentication=AwsRdsIam", tokenProvider: new CountingTokenProvider("unused"));
            var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
            var retentionLockDataSource = provider.GetRequiredKeyedService<NpgsqlDataSource>(PostgreSqlRetentionLock.TokenDataSourceKey);

            await provider.DisposeAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.ThrowsAsync<ObjectDisposedException>(async () => await dataSource.OpenConnectionAsync());
                Assert.ThrowsAsync<ObjectDisposedException>(async () => await retentionLockDataSource.OpenConnectionAsync());
            }
        }

        internal static ServiceProvider BuildServiceProvider(string connectionString, string schema = null, IPostgreSqlTokenProvider tokenProvider = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var persistence = new PostgreSqlPersistenceConfiguration().Create(CreateSettings(connectionString, schema));
            persistence.AddInstaller(services);
            persistence.AddPersistence(services);

            if (tokenProvider is not null)
            {
                services.AddSingleton(tokenProvider);
            }

            return services.BuildServiceProvider();
        }

        internal static Task ApplyMigrations(IServiceProvider provider) => ActivatorUtilities.CreateInstance<DatabaseSetup>(provider).StartAsync();

        internal static async Task RoundTrip(IServiceProvider provider)
        {
            var id = Guid.NewGuid();

            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
                dbContext.FailedAuditImports.Add(new FailedAuditImportEntity { UniqueMessageId = id, FailedAt = DateTime.UtcNow, HeadersJson = "{}", Body = [1, 2, 3] });
                await dbContext.SaveChangesAsync();
            }

            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
                var stored = await dbContext.FailedAuditImports.AsNoTracking().SingleAsync(e => e.UniqueMessageId == id);

                Assert.That(stored.Body, Is.EqualTo(new byte[] { 1, 2, 3 }));
            }
        }

        static async Task QueryOnConcurrentConnections(IServiceProvider provider, int connections)
        {
            var scopes = Enumerable.Range(0, connections).Select(_ => provider.CreateAsyncScope()).ToArray();

            try
            {
                foreach (var scope in scopes)
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
                    await dbContext.Database.OpenConnectionAsync();
                    await dbContext.FailedAuditImports.CountAsync();
                }
            }
            finally
            {
                foreach (var scope in scopes)
                {
                    await scope.DisposeAsync();
                }
            }
        }

        static object[] DataSourceKeys(string connectionString)
        {
            var services = new ServiceCollection();
            var persistence = new PostgreSqlPersistenceConfiguration().Create(CreateSettings(connectionString));
            persistence.AddPersistence(services);
            persistence.AddInstaller(services);

            return [.. services.Where(descriptor => descriptor.ServiceType == typeof(NpgsqlDataSource)).Select(descriptor => descriptor.ServiceKey)];
        }

        static PersistenceSettings CreateSettings(string connectionString, string schema = null)
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] = connectionString;

            if (schema is not null)
            {
                settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.SchemaKey] = schema;
            }

            return settings;
        }

        sealed class CountingTokenProvider(string password) : IPostgreSqlTokenProvider
        {
            int calls;

            public int Calls => Volatile.Read(ref calls);

            public string Username { get; private set; }

            public ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref calls);
                Username = settings.Username;

                return ValueTask.FromResult(password);
            }
        }
    }
}
