namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.AcceptanceTesting.InfrastructureConfig;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.Migration.Checks;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.Tests;
using ServiceControl.Persistence.UnitOfWork;
using TestHelper;
// Aliased rather than imported: Raven.Client.Documents carries LINQ extensions that collide with EF Core's.
using IDocumentStore = Raven.Client.Documents.IDocumentStore;

// No base class: the persistence test bases sit in projects that cannot see RavenDB.
abstract class MigrationAcceptanceTest
{
    protected const string EndpointSettingsUrl = "api/endpointssettings";

    readonly AcceptanceTestStorageConfiguration StorageConfiguration = new();
    readonly List<string> setVariables = [];
    CancellationTokenSource hostCancellation;
    Task runningHost;

    ServiceProvider targetServices;
    IMigrationCheckpointStore targetCheckpointStore;

    protected Settings Settings { get; private set; }
    protected HttpClient HttpClient { get; private set; }
    protected (string ServerUrl, string PrimaryDatabase, string ThroughputDatabase) Source { get; private set; }
    protected IDocumentStore SourceStore { get; private set; }

    protected IMigrationTarget Target { get; private set; }

    protected InMemoryBodyStoragePersistence RecordedBodies { get; } = new();

    [SetUp]
    public async Task SetUp()
    {
        Source = await MigrationSourceServer.CreateDatabases();
        SourceStore = await (await MigrationSourceServer.GetInstance()).Connect();

        await SeedSourceDataVersion();

        SetSourceVariable("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", Source.ServerUrl);
        SetSourceVariable("SERVICECONTROL_RAVENDB_DATABASENAME", Source.PrimaryDatabase);
        SetSourceVariable("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", Source.ThroughputDatabase);
        SetSourceVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "true");

        // Settings.Port has no public setter, so the port has to come in as the environment variable a customer would set.
        SetSourceVariable("SERVICECONTROL_PORT", PortUtility.GetAssignedOrAvailablePort(33500).ToString(CultureInfo.InvariantCulture));

        var transport = new ConfigureEndpointLearningTransport();

        Settings = new Settings(
            transportType: transport.TypeName,
            persisterType: StorageConfiguration.PersistenceType,
            forwardErrorMessages: false,
            errorRetentionPeriod: TimeSpan.FromDays(10))
        {
            TransportConnectionString = transport.ConnectionString
        };

        await StorageConfiguration.CustomizeSettings(Settings);
        await new SetupCommand().Execute(new HostArguments([]), Settings);

        HttpClient = new HttpClient { BaseAddress = new Uri(Settings.RootUrl) };

        var targetServiceCollection = new ServiceCollection();
        targetServiceCollection.AddLogging();
        targetServiceCollection.AddPersistence(Settings);
        targetServiceCollection.AddSingleton<IBodyStoragePersistence>(RecordedBodies);
        targetServices = targetServiceCollection.BuildServiceProvider();

        Target = targetServices.GetRequiredService<IMigrationTarget>();
        targetCheckpointStore = targetServices.GetRequiredService<IMigrationCheckpointStore>();
        await Target.Open();
    }

    // The variables go first because they are process wide: a failure in the cleanup below must not leave them set for the next test.
    [TearDown]
    public async Task TearDown()
    {
        foreach (var name in setVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        try
        {
            await StopHost();
        }
        finally
        {
            HttpClient?.Dispose();
            SourceStore?.Dispose();

            if (targetServices is not null)
            {
                await targetServices.DisposeAsync();
            }

            await StorageConfiguration.Cleanup();
        }
    }

    protected void SetSourceVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        setVariables.Add(name);
    }

    protected async Task SeedSource(string database, params (string Id, object Document)[] documents)
    {
        using var session = SourceStore.OpenAsyncSession(database);

        foreach (var (id, document) in documents)
        {
            await session.StoreAsync(document, id);
        }

        await session.SaveChangesAsync();
    }

    protected Task SeedSource(params (string Id, object Document)[] documents) =>
        SeedSource(Source.PrimaryDatabase, documents);

    protected Task SeedSourceEndpointSettings(params (string Name, bool TrackInstances)[] settings) =>
        SeedSource([.. settings.Select(setting => ($"EndpointSettings/{DeterministicGuid.MakeId(setting.Name)}", (object)new EndpointSettings { Name = setting.Name, TrackInstances = setting.TrackInstances }))]);

    // The target copies an endpoint setting only when that endpoint is known, so seed the endpoint too.
    protected Task SeedSourceKnownEndpoints(params string[] names) =>
        SeedSource([.. names
            .Select(name => new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = name, HostId = Guid.NewGuid(), Host = "HOST01" } })
            .Select(endpoint => ($"KnownEndpoints/{endpoint.EndpointDetails.GetDeterministicId()}", (object)endpoint))]);

    // What DatabaseSetup.StampDataVersion writes on a real instance, in both databases because the
    // check reads both. Without it every startup check refuses.
    protected async Task SeedSourceDataVersion(string version = null)
    {
        foreach (var database in new[] { Source.PrimaryDatabase, Source.ThroughputDatabase })
        {
            await SeedSource(database, (RavenDataVersionDocumentId, new SourceDataVersion { Version = version ?? ThisBuildVersion, StampedAt = DateTime.UtcNow }));
        }
    }

    // Spelled out rather than referenced: the acceptance projects cannot see the RavenDB persister's types.
    const string RavenDataVersionDocumentId = "ServiceControl/DataVersion";

    static readonly string ThisBuildVersion =
        typeof(Settings).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(Settings).Assembly.GetName().Version!.ToString(3);

    protected Task<IMigrationSource> OpenSource(CancellationToken cancellationToken = default) =>
        PersistenceFactory.OpenMigrationSource(Settings, cancellationToken);

    static IReadOnlyCollection<string> sourceSupportedCategoryIds;

    // SupportedCategoryIds answers before Open and cannot change across it, so one source built once for the
    // whole run answers it for every test instead of loading the persister assembly again per test.
    protected async Task<IReadOnlyCollection<string>> SourceSupportedCategoryIds()
    {
        if (sourceSupportedCategoryIds is null)
        {
            await using var source = PersistenceFactory.CreateMigrationSource(Settings);
            sourceSupportedCategoryIds = source.SupportedCategoryIds;
        }

        return sourceSupportedCategoryIds;
    }


    // This build cannot copy every required category yet, so without the marker every host test is refused at startup.
    protected static Action<WebApplicationBuilder> AllowingAnIncompleteCategorySet(Action<WebApplicationBuilder> customize = null) =>
        builder =>
        {
            builder.Services.AddSingleton<AllowIncompleteCategorySet>();
            customize?.Invoke(builder);
        };

    protected async Task RunHostUntilTheApiAnswers(Action<WebApplicationBuilder> customize = null)
    {
        await StopHost();

        hostCancellation = new CancellationTokenSource();
        runningHost = RunCommand.Run(Settings, AllowingAnIncompleteCategorySet(customize), hostCancellation.Token);

        await WaitForEndpointSettingsResponse(TimeSpan.FromMinutes(2));
    }

    protected Settings SettingsWithMigrationEnabled(string optionalCategories = null, Action<Settings> customize = null)
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "true");
        SetSourceVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", optionalCategories);
        customize?.Invoke(Settings);
        return Settings;
    }

    protected async Task RunRequiredCopy(
        Action<IServiceCollection> customize = null,
        bool leaveOptionalIncomplete = false,
        CancellationToken cancellationToken = default)
    {
        SettingsWithMigrationEnabled(optionalCategories: leaveOptionalIncomplete ? "EventLog" : null);

        await RunHostUntilTheApiAnswers(builder => customize?.Invoke(builder.Services));

        var copyable = MigrationStartup.CopyableCategoryIds(await SourceSupportedCategoryIds(), Target.SupportedCategoryIds);

        var copied = MigrationCategoryRegistry.All
            .Where(category => category.Kind == MigrationCategoryKind.Required && copyable.Contains(category.Id))
            .Select(category => category.Id)
            .ToArray();

        await WaitUntil(
            async () => (await targetCheckpointStore.ReadAll(cancellationToken))
                .Count(checkpoint => copied.Contains(checkpoint.CategoryId) && checkpoint.State.IsFinished()) == copied.Length,
            "the required copy finished");

        if (leaveOptionalIncomplete)
        {
            // The guarded services read IMigrationState.AnyCategoryIncomplete, which any selected category that has not finished holds true. Halted is one of those states.
            await targetCheckpointStore.Upsert(
                new MigrationCheckpoint("EventLog", MigrationCategoryState.Halted, null, 0, 0, null, null, null, null, null,
                    "Halted by the test fixture so the guarded services see an unfinished category."),
                cancellationToken);
        }
    }

    protected async Task<IReadOnlyList<EndpointSettings>> WaitForEndpointSettingsResponse(TimeSpan timeout)
    {
        IReadOnlyList<EndpointSettings> settings = null;

        await WaitUntil(async () =>
        {
            // A host that refused to start surfaces its own exception here instead of waiting out the timeout.
            if (runningHost is { IsFaulted: true })
            {
                await runningHost;
            }

            try
            {
                settings = await GetEndpointSettings();
                return true;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }, $"GET {EndpointSettingsUrl} answered", timeout);

        return settings;
    }

    protected async Task<IReadOnlyList<EndpointSettings>> GetEndpointSettings() =>
        await HttpClient.GetFromJsonAsync<List<EndpointSettings>>(EndpointSettingsUrl, SerializerOptions.Default);

    async Task StopHost()
    {
        if (runningHost is null)
        {
            return;
        }

        await hostCancellation.CancelAsync();
        await runningHost;
        hostCancellation.Dispose();
        runningHost = null;
    }

    // Copied from PersistenceTestBase.WaitUntil, which this project cannot compile.
    protected static async Task WaitUntil(Func<Task<bool>> conditionChecker, string condition, TimeSpan timeout = default)
    {
        timeout = timeout == default ? TimeSpan.FromSeconds(10) : timeout;

        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (await conditionChecker())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new Exception($"{condition} has not been meet in defined timespan: {timeout})");
    }

    // Copied from RavenMigrationSourceTestBase.CollectBatches, which this project cannot compile.
    protected static async Task<List<MigrationBatch>> CollectBatches(IMigrationSource source, MigrationCategory category, string resumeAfter = null, int batchSize = 100)
    {
        var batches = new List<MigrationBatch>();

        await foreach (var batch in source.Read(category, resumeAfter, batchSize, TestContext.CurrentContext.CancellationToken))
        {
            batches.Add(batch);
        }

        return batches;
    }

    protected IEndpointSettingsStore EndpointSettingsStore => targetServices.GetRequiredService<IEndpointSettingsStore>();
    protected IMonitoringDataStore MonitoringDataStore => targetServices.GetRequiredService<IMonitoringDataStore>();

    protected async Task<MigrationWriteResult> CopyCategory(string categoryId, CancellationToken cancellationToken = default)
    {
        var category = MigrationCategoryRegistry.Find(categoryId) ?? throw new ArgumentException($"'{categoryId}' is not a migration category.", nameof(categoryId));
        var before = await targetCheckpointStore.Read(categoryId, cancellationToken);
        var target = new RecordingMigrationTarget(Target);
        var options = new MigrationEngineOptions(TimeSpan.Zero, MigrationSettings.DefaultHaltThresholdPercent, MigrationSettings.DefaultHaltThresholdMinimum, []) { BodyRetryBackoff = TimeSpan.Zero };

        await using var source = await OpenSource(cancellationToken);
        var engine = new MigrationEngine(source, target, targetCheckpointStore, TimeProvider.System, options, NullLogger<MigrationEngine>.Instance);

        var after = await engine.RunCategoryAsync(category, cancellationToken);

        if (!after.State.IsFinished())
        {
            throw new InvalidOperationException($"Copying '{categoryId}' ended {after.State}: {after.LastError}");
        }

        return new MigrationWriteResult(
            after,
            (int)(after.CopiedCount - (before?.CopiedCount ?? 0)),
            (int)(after.SkippedCount - (before?.SkippedCount ?? 0)),
            target.SkippedIds,
            (int)(after.AlreadyPresentCount - (before?.AlreadyPresentCount ?? 0)));
    }


    // Makes the target look like a database a migration has touched, which is the only state the host-opened marker is stamped in.
    protected async Task SeedCheckpoint(string categoryId, MigrationCategoryState state = MigrationCategoryState.Complete)
    {
        await using var scope = targetServices.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        await dbContext.UpsertCheckpoint(new MigrationCheckpoint(categoryId, state, null, 0, 0, null, null, null, null, null, null));
    }

    protected async Task DeleteCheckpoint(string categoryId)
    {
        await using var scope = targetServices.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        await dbContext.MigrationCheckpoints.Where(checkpoint => checkpoint.CategoryId == categoryId).ExecuteDeleteAsync();
    }

    protected async Task<T> QueryTarget<T>(Func<ServiceControlDbContext, Task<T>> query)
    {
        using var scope = targetServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await query(dbContext);
    }

    protected async Task<FailedMessageEntity> GetFailedMessage(Guid uniqueMessageId)
    {
        var row = await QueryTarget(dbContext => dbContext.FailedMessages.AsNoTracking().SingleOrDefaultAsync(m => m.UniqueMessageId == uniqueMessageId));

        Assert.That(row, Is.Not.Null, $"No failed message row for {uniqueMessageId}");

        return row;
    }

    protected Task<FailedMessageEntity> FindFailedMessage(Guid uniqueMessageId) =>
        QueryTarget(dbContext => dbContext.FailedMessages.AsNoTracking().SingleOrDefaultAsync(m => m.UniqueMessageId == uniqueMessageId));

    protected EFPersisterSettings EFSettings => targetServices.GetRequiredService<EFPersisterSettings>();

    protected Task Ingest(params IngestedFailure[] failures) =>
        InBatch(async unitOfWork =>
        {
            foreach (var failure in failures)
            {
                await unitOfWork.Recoverability.RecordFailedProcessingAttempt(failure.Context, failure.ProcessingAttempt, failure.Groups);
            }
        });

    // Ingestion takes the body and its content type from the message context, so only the context changes.
    protected Task IngestWithBody(IngestedFailure failure, byte[] body, string contentType) =>
        InBatch(unitOfWork =>
        {
            var headers = new Dictionary<string, string>(failure.Headers) { [NServiceBus.Headers.ContentType] = contentType };
            var context = new MessageContext(failure.MessageId, headers, body, new TransportTransaction(), "receiveAddress", new ContextBag());

            return unitOfWork.Recoverability.RecordFailedProcessingAttempt(context, failure.ProcessingAttempt, failure.Groups);
        });

    async Task InBatch(Func<IIngestionUnitOfWork, Task> record)
    {
        await using var unitOfWork = await targetServices.GetRequiredService<IIngestionUnitOfWorkFactory>().StartNew();

        await record(unitOfWork);

        await unitOfWork.Complete(TestContext.CurrentContext.CancellationToken);
    }

    protected Task<MigrationCheckpoint> ReadCheckpoint(string categoryId) =>
        QueryTarget(async dbContext =>
        {
            var row = await dbContext.MigrationCheckpoints.AsNoTracking().SingleOrDefaultAsync(checkpoint => checkpoint.CategoryId == categoryId);

            return row is null ? null : new MigrationCheckpoint(
                row.CategoryId, row.State, row.Cursor,
                row.CopiedCount, row.SkippedCount, row.SourceTotal, row.SkipReasons,
                row.StartedAt, row.LastProgressAt, row.SettledAt, row.LastError,
                row.AlreadyPresentCount, row.Version);
        });

    protected Task<string> ReadSetting(string key) =>
        QueryTarget(dbContext => dbContext.Settings.AsNoTracking()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync());

    sealed class RecordingMigrationTarget(IMigrationTarget inner) : IMigrationTarget
    {
        public List<string> SkippedIds { get; } = [];

        public Task Open(CancellationToken cancellationToken = default) => inner.Open(cancellationToken);

        public int BatchSizeFor(MigrationCategory category) => inner.BatchSizeFor(category);

        public async Task<MigrationWriteResult> Write(MigrationCategory category, MigrationBatch batch, MigrationCheckpoint checkpointToExtend, CancellationToken cancellationToken = default)
        {
            var result = await inner.Write(category, batch, checkpointToExtend, cancellationToken);
            SkippedIds.AddRange(result.SkippedIds);
            return result;
        }

        public Task<long> Count(MigrationCategory category, CancellationToken cancellationToken = default) => inner.Count(category, cancellationToken);

        public IReadOnlyCollection<string> SupportedCategoryIds => inner.SupportedCategoryIds;
    }

    // The stamp the RavenDB persister reads back, spelled out because this project cannot see its type.
    sealed class SourceDataVersion
    {
        public string Version { get; set; }

        public DateTime StampedAt { get; set; }
    }
}
