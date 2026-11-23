namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Unicast.Subscriptions;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents.Operations.Indexes;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Infrastructure.RavenDB.Subscriptions;
using ServiceControl.Notifications;
using ServiceControl.Operations;
using ServiceControl.Configuration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.DataMigration;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.RavenDB.Throughput.Models;
using RavenLicensingDataStore = Persistence.RavenDB.Throughput.LicensingDataStore;

class RavenMigrationSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Configuration_names_the_mode_server_and_both_databases_before_open()
    {
        var settings = (RavenPersisterSettings)PersistenceSettings;
        await using var source = CreateMigrationSource();

        Assert.That(source.Configuration, Is.EqualTo(new MigrationSettingValue[]
        {
            new("Mode", "external", "ServiceControl/RavenDB/ConnectionString"),
            new("Server", settings.ConnectionString, "ServiceControl/RavenDB/ConnectionString"),
            new("Primary database", settings.DatabaseName, "ServiceControl/RavenDB/DatabaseName"),
            new("Throughput database", settings.ThroughputDatabaseName, "LicensingComponent/RavenDB/ThroughputDatabaseName")
        }));
    }

    [Test]
    public async Task An_embedded_source_names_its_data_directory_before_open()
    {
        var embedded = new RavenPersisterSettings
        {
            DatabasePath = @"C:\ServiceControl\DB",
            DatabaseName = "servicecontrol",
            ThroughputDatabaseName = "throughput"
        };
        await using var source = new RavenMigrationSource(new RavenReadOnlySourceLifecycle(embedded, new SettingsRootNamespace("ServiceControl")));

        Assert.That(source.Configuration.Select(value => (value.Label, value.Value)), Is.EqualTo(new[]
        {
            ("Mode", "embedded"),
            ("Server", embedded.ServerUrl),
            ("Data directory", @"C:\ServiceControl\DB"),
            ("Primary database", "servicecontrol"),
            ("Throughput database", "throughput")
        }));
    }

    [Test]
    public async Task Reads_endpoint_settings_in_document_id_order_in_batches_of_the_requested_size()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        var batches = await CollectBatches(source, EndpointSettingsCategory, batchSize: 2);

        Assert.Multiple(() =>
        {
            Assert.That(batches.Select(batch => batch.Rows.Count), Is.EqualTo(new[] { 2, 2, 1 }), "A batch never exceeds the requested size, and the trailing partial batch is still yielded.");
            Assert.That(batches.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.Ordered);
        });
    }

    [Test]
    public async Task Every_row_is_identified_by_the_document_id_the_persister_wrote_it_under()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        var rows = (await CollectBatches(source, EndpointSettingsCategory)).SelectMany(batch => batch.Rows).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(5));
            Assert.That(rows.Select(row => row.SourceId), Has.All.StartWith("EndpointSettings/"), "The cursor the engine checkpoints is a source document id, so a row identified by anything else cannot be resumed after.");
        });
    }

    [Test]
    public async Task Resuming_after_a_cursor_yields_only_what_follows_it()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        var firstBatch = (await CollectBatches(source, EndpointSettingsCategory, batchSize: 2))[0];
        var resumed = (await CollectBatches(source, EndpointSettingsCategory, resumeAfter: firstBatch.Cursor, batchSize: 2)).SelectMany(batch => batch.Rows).ToList();

        Assert.That(resumed, Has.Count.EqualTo(3));
        Assert.That(resumed.Select(row => row.SourceId), Has.No.Member(firstBatch.Rows[0].SourceId));
        Assert.That(resumed.Select(row => row.SourceId), Is.Ordered);
    }

    [Test]
    public async Task Resuming_after_a_cursor_this_source_never_issued_refuses_instead_of_starting_past_it()
    {
        // The cursor is saved in the target database, so a restored backup or a corrected database name leaves
        // one naming a document this source does not have.
        await SeedEndpointSettings(3);
        await using var source = await OpenMigrationSource();

        var refusal = Assert.ThrowsAsync<InvalidOperationException>(() => CollectBatches(source, EndpointSettingsCategory, resumeAfter: "EndpointSettings/zzz-from-another-database"));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(EndpointSettingsCategory.Id));
            Assert.That(refusal.Message, Does.Contain("EndpointSettings/zzz-from-another-database"));
            Assert.That(refusal.Message, Does.Contain(DatabaseName), "the operator has to be told which database the cursor was looked for in");
            Assert.That(refusal.Message, Does.Contain("expired"), "an archived or resolved document can expire during an interrupted copy, on any source");
            Assert.That(refusal.Message, Does.Contain($"--start-data-migration --retry={EndpointSettingsCategory.Id}"), "a retry re-reads the category from the start, so it is the way out");
        });
    }

    [Test]
    public async Task Counts_every_row_the_category_would_read()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        Assert.That(await source.Count(EndpointSettingsCategory), Is.EqualTo(5));
    }

    [Test]
    public async Task Counts_a_category_with_nothing_in_it_as_zero()
    {
        await using var source = await OpenMigrationSource();

        Assert.That(await source.Count(EndpointSettingsCategory), Is.Zero, "A category with nothing to copy has to be told apart from one this source declines to count.");
    }

    [Test]
    public async Task Reading_every_category_creates_no_index()
    {
        await using var source = await OpenMigrationSource();

        Assert.That(source.SupportedCategoryIds, Is.SubsetOf(Seeds.Keys), "A category read with nothing in it cannot show whether its reader builds an index, so a new reader needs its seed adding here.");

        foreach (var categoryId in source.SupportedCategoryIds)
        {
            await Seeds[categoryId](this);
        }

        var before = await IndexNames();

        foreach (var categoryId in source.SupportedCategoryIds)
        {
            await CollectBatches(source, MigrationCategoryRegistry.Find(categoryId));
        }

        var after = await IndexNames();

        Assert.Multiple(() =>
        {
            Assert.That(after, Is.EquivalentTo(before), "A reader that queries without naming a static index has RavenDB build one and index the whole collection on the customer's live database.");
            Assert.That(after, Has.None.StartWith("Auto/"));
        });
    }

    // Keyed by category id so a reader added without seed data fails the index test by name instead of passing
    // over an empty collection.
    static readonly Dictionary<string, Func<RavenMigrationSourceTests, Task>> Seeds = new()
    {
        [MigrationCategoryIds.EndpointSettings] = tests => tests.SeedEndpointSettings(2),
        [MigrationCategoryIds.KnownEndpoints] = tests => tests.MonitoringDataStore.CreateIfNotExists(new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "HOST01" }),
        [MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages] = tests => tests.SeedFailedMessage(new IngestedFailure().ToFailedMessage()),
        [MigrationCategoryIds.LicensingEndpoints] = tests => tests.SeedThroughputDocument("Sales.Orders/Broker", new EndpointDocument(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker))),
        [MigrationCategoryIds.LicensingThroughput] = tests => tests.SeedThroughputDocument("Sales.Orders/Broker", new EndpointDocument(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker))),
        [MigrationCategoryIds.LicensingReportMasks] = tests => tests.SeedThroughputDocument(RavenLicensingDataStore.ReportMasksDocumentId, new ReportConfigurationDocument { MaskedStrings = ["contoso"] }),
        [MigrationCategoryIds.LicensedEndpointDetails] = tests => tests.SeedThroughputDocument(RavenLicensingDataStore.LicencedEndpointDetailsDocumentId, new LicensedEndpointDetails { LicenseId = "abc-123" }),
        [MigrationCategoryIds.MessageRedirects] = tests => tests.MessageRedirectsDataStore.AddRedirect(new MessageRedirect { FromPhysicalAddress = "Sales@OLDHOST", ToPhysicalAddress = "Sales@NEWHOST", LastModified = DateTime.UtcNow }),
        [MigrationCategoryIds.Subscriptions] = tests => tests.SeedSubscription(),
        [MigrationCategoryIds.NotificationSettings] = tests => tests.NotificationsStore.SaveSettings(new NotificationsSettings { Email = new EmailNotifications { SmtpServer = "smtp.contoso.com" } }),
        [MigrationCategoryIds.TrialEndDate] = tests => tests.ServiceProvider.GetRequiredService<ITrialLicenseDataProvider>().StoreTrialEndDate(new DateOnly(2027, 3, 15)),
        [MigrationCategoryIds.RetryOperations] = tests => tests.RetryHistoryStore.RecordRetryOperationCompleted("group-1", RetryType.FailureGroup, DateTime.UtcNow, DateTime.UtcNow, "Sales.Orders", "Exception Type", false, 1, DateTime.UtcNow, 10),
        [MigrationCategoryIds.PendingIntegrationEvents] = tests => tests.ExternalIntegrationStore.StoreDispatchRequest([new ExternalIntegrationDispatchRequest { DispatchContext = new TestDispatchContext { Value = "seed" } }]),
        [MigrationCategoryIds.CustomChecks] = tests => tests.CustomChecks.UpdateCustomCheckStatus(new CustomCheckDetail { OriginatingEndpoint = new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "HOST01" }, CustomCheckId = "Sales.HealthCheck", Category = "Ping" }),
        [MigrationCategoryIds.FailedErrorImports] = tests => tests.FailedImportStore.StoreFailedErrorImport(new FailedErrorImport { Id = "native-1", Message = new FailedTransportMessage { Id = "native-1", Headers = new Dictionary<string, string> { ["NServiceBus.MessageId"] = "native-1" }, Body = [1, 2, 3] }, ExceptionInfo = "Deserialization failed" }),
        [MigrationCategoryIds.GroupComments] = tests => tests.GroupsStore.EditComment("group-1", "Known issue, ignore until Thursday")
    };

    string DatabaseName => ((RavenPersisterSettings)PersistenceSettings).DatabaseName;

    // Both databases, because the licensing readers read the throughput database.
    async Task<string[]> IndexNames() =>
        [.. await IndexNamesIn(DatabaseName), .. await IndexNamesIn(ThroughputDatabaseName)];

    Task<string[]> IndexNamesIn(string database) =>
        DocumentStore.Maintenance.ForDatabase(database).SendAsync(new GetIndexNamesOperation(0, IndexNamePageSize), TestContext.CurrentContext.CancellationToken);

    const int IndexNamePageSize = 1024;

    static readonly MigrationCategory EndpointSettingsCategory = MigrationCategoryRegistry.Find("EndpointSettings");

    async Task SeedThroughputDocument(string id, object document)
    {
        using var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName);
        await session.StoreAsync(document, id);
        await session.SaveChangesAsync();
    }

    async Task SeedSubscription()
    {
        var document = new Subscriptions();
        document.All["v1"] = new Subscription
        {
            Id = "v1",
            MessageType = new MessageType("MyMessages.OrderPlaced", new Version(1, 0, 0)),
            Subscribers = [new SubscriptionClient { Endpoint = "Billing", TransportAddress = "Billing@BILLING01" }]
        };

        using var session = await SessionProvider.OpenSession();
        await session.StoreAsync(document, Subscriptions.SingleDocumentId);
        await session.SaveChangesAsync();
    }

    async Task SeedEndpointSettings(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = $"Endpoint{index}", TrackInstances = true });
        }
    }

    public class TestDispatchContext
    {
        public string Value { get; set; }
    }
}
