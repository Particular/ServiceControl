// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EFCore.Entities;
using EFCore.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.PostgreSql.TokenAuthentication;

[TestFixture]
class TokenAuthenticationTests
{
    [TestCase("Token Authentication=AzureEntraId;Token Scope=https://ossrdbms-aad.database.windows.net/.default")]
    [TestCase("Token Authentication=AwsRdsIam")]
    public async Task Migrates_and_queries_with_one_token_for_many_connections(string keywords)
    {
        var (connectionString, password) = await TokenConnectionStrings.ForSharedContainer(keywords);
        var settings = CreateSettings(connectionString, $"sc_token_{Guid.NewGuid():n}");
        var tokenProvider = new CountingTokenProvider(password);

        try
        {
            await using var provider = BuildServiceProvider(settings, tokenProvider);

            await ApplyMigrations(provider);
            await RoundTrip(provider);
            await QueryOnConcurrentConnections(provider, connections: 3);
            var hosting = await provider.GetRequiredService<IDatabaseHostingProbe>().Probe();
            var identity = await provider.GetRequiredService<IStorageIdentityProvider>().GetIdentity();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hosting.Source, Is.EqualTo(DatabaseHostingSource.Probe));
                Assert.That(identity?.Schema, Is.EqualTo(settings.Schema));
                Assert.That(tokenProvider.Calls, Is.EqualTo(1));
                Assert.That(tokenProvider.Username, Is.EqualTo(new NpgsqlConnectionStringBuilder(settings.ConnectionString).Username));
            }
        }
        finally
        {
            await TestSchema.Drop(await PostgreSqlSharedContainer.GetConnectionStringAsync(), settings.Schema);
        }
    }

    [Test]
    public async Task Logs_a_failure_to_get_a_token()
    {
        var (connectionString, _) = await TokenConnectionStrings.ForSharedContainer("Token Authentication=AwsRdsIam");
        _ = PostgreSqlTokenAuthentication.Parse(connectionString, "Database/ConnectionString", out var npgsqlConnectionString);
        var failure = new InvalidOperationException("No credentials");
        var logger = new RecordingLogger();
        await using var dataSource = PostgreSqlTokenDataSource.Build(npgsqlConnectionString, new FailingTokenProvider(failure), logger);

        Assert.CatchAsync(async () => await dataSource.OpenConnectionAsync());

        Assert.That(logger.Errors, Does.Contain(failure));
    }

    [Test]
    public void Registers_a_data_source_only_with_token_authentication()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RegisteredServices("Host=localhost;Username=sc"), Has.None.EqualTo(typeof(NpgsqlDataSource)));
            Assert.That(RegisteredServices("Host=localhost;Username=sc;Token Authentication=AwsRdsIam"), Has.One.EqualTo(typeof(NpgsqlDataSource)));
        }
    }

    [Test]
    public async Task Disposes_the_data_source_with_the_container()
    {
        var provider = BuildServiceProvider(CreateSettings("Host=localhost;Username=sc;Token Authentication=AwsRdsIam"), new CountingTokenProvider("unused"));
        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();

        await provider.DisposeAsync();

        Assert.ThrowsAsync<ObjectDisposedException>(async () => await dataSource.OpenConnectionAsync());
    }

    internal static PostgreSqlPersisterSettings CreateSettings(string connectionString, string schema = null) => new()
    {
        ConnectionString = connectionString,
        Schema = schema,
        BodyStorage = new FileSystemBodyStorageSettings { StoragePath = Path.GetTempPath() }
    };

    internal static ServiceProvider BuildServiceProvider(PostgreSqlPersisterSettings settings, IPostgreSqlTokenProvider tokenProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var persistence = new PostgreSqlPersistenceConfiguration().Create(settings);
        persistence.AddInstaller(services);
        persistence.AddPersistence(services);

        if (tokenProvider is not null)
        {
            services.AddSingleton(tokenProvider);
        }

        return services.BuildServiceProvider();
    }

    internal static async Task ApplyMigrations(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().ApplyMigrations();
    }

    internal static async Task RoundTrip(IServiceProvider provider)
    {
        var name = $"token-{Guid.NewGuid():n}";

        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            dbContext.EndpointSettings.Add(new EndpointSettingsEntity { Name = name, TrackInstances = true });
            await dbContext.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var stored = await dbContext.EndpointSettings.AsNoTracking().SingleAsync(e => e.Name == name);

            Assert.That(stored.TrackInstances, Is.True);
        }
    }

    static async Task QueryOnConcurrentConnections(IServiceProvider provider, int connections)
    {
        var scopes = Enumerable.Range(0, connections).Select(_ => provider.CreateAsyncScope()).ToArray();

        try
        {
            foreach (var scope in scopes)
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
                await dbContext.Database.OpenConnectionAsync();
                await dbContext.EndpointSettings.CountAsync();
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

    static Type[] RegisteredServices(string connectionString)
    {
        var services = new ServiceCollection();
        var persistence = new PostgreSqlPersistenceConfiguration().Create(CreateSettings(connectionString));
        persistence.AddPersistence(services);
        persistence.AddInstaller(services);

        return [.. services.Select(descriptor => descriptor.ServiceType)];
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

    sealed class FailingTokenProvider(Exception failure) : IPostgreSqlTokenProvider
    {
        public ValueTask<string> GetToken(NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken = default) => ValueTask.FromException<string>(failure);
    }

    sealed class RecordingLogger : ILogger
    {
        readonly ConcurrentQueue<Exception> errors = new();

        public Exception[] Errors => [.. errors];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                errors.Enqueue(exception);
            }
        }
    }
}
