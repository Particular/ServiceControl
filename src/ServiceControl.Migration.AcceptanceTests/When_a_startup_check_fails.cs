namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Abstractions;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_a_startup_check_fails : MigrationAcceptanceTest
{
    const string HostOpenedSetting = "Migration/HostOpenedOnTarget";
    const string DataVersionDocumentId = "ServiceControl/DataVersion";

    [Test]
    public async Task An_unknown_category_name_is_refused()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "NotACategory");

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the selected categories are coherent").And.Contain("NotACategory"));
    }

    [Test]
    public async Task A_source_that_is_not_RavenDB_is_refused()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_SOURCEPERSISTENCETYPE", "SQLServer");

        var refusal = await RefusedStartup();

        // Naming both settings is what proves this ran ahead of PersistenceFactory.CreateMigrationSource, whose own refusal names only the source.
        Assert.That(refusal.Message, Does.Contain("the source and target are the supported pair")
            .And.Contain("Migration/SourcePersistenceType")
            .And.Contain("PersistenceType"));
    }

    [Test]
    public async Task A_retry_history_depth_that_empties_the_migrated_table_is_refused()
    {
        // Assigned rather than set as an environment variable: Settings reads RetryHistoryDepth once, in the constructor the fixture has already run.
        Settings.RetryHistoryDepth = 0;

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the retry history depth will not empty a migrated table")
            .And.Contain("RetryHistoryDepth")
            .And.Contain("HistoricRetryOperations"));
    }

    [Test]
    public async Task A_target_whose_schema_migrations_were_never_applied_is_refused()
    {
        await QueryTarget(async dbContext =>
        {
            // EF's own history repository, because it is the only thing that knows where the history table is once the persister is given a schema.
            var history = dbContext.GetService<IHistoryRepository>();
            var applied = await history.GetAppliedMigrationsAsync();

            foreach (var row in applied)
            {
                await dbContext.Database.ExecuteSqlRawAsync(history.GetDeleteScript(row.MigrationId));
            }

            return applied.Count;
        });

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the target schema is current").And.Contain("--setup"));
    }

    [Test]
    public async Task Message_body_storage_that_cannot_be_written_to_is_refused()
    {
        var parentThatIsAFile = Path.Combine(Path.GetTempPath(), $"sc-not-a-directory-{Guid.NewGuid():n}");
        File.WriteAllText(parentThatIsAFile, string.Empty);

        try
        {
            ((FileSystemBodyStorageSettings)EFSettings.BodyStorage).StoragePath = Path.Combine(parentThatIsAFile, "bodies");

            var refusal = await RefusedStartup();

            Assert.That(refusal.Message, Does.Contain("message body storage is writable"));
        }
        finally
        {
            File.Delete(parentThatIsAFile);
        }
    }

    [Test]
    public async Task A_client_certificate_that_has_expired_is_refused()
    {
        var notBefore = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notAfter = new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);

        SetSourceVariable("SERVICECONTROL_RAVENDB_CLIENTCERTIFICATEBASE64", ExpiredClientCertificate(notBefore, notAfter));

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the migration source opens")
            .And.Contain($"{notBefore.UtcDateTime:u} to {notAfter.UtcDateTime:u}"));
    }

    [Test]
    public async Task A_throughput_database_that_does_not_exist_is_refused()
    {
        var missing = $"{Source.ThroughputDatabase}-does-not-exist";
        SetSourceVariable("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", missing);

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the migration source opens")
            .And.Contain(missing)
            .And.Contain("LicensingComponent/RavenDB/ThroughputDatabaseName"));
    }

    [Test]
    public async Task A_source_that_carries_no_data_version_stamp_is_refused()
    {
        await DeleteSourceDataVersion();

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the source is at a data version this build can read")
            .And.Contain("carries no ServiceControl data version stamp"));
    }

    // The case a customer reaches by upgrading their binaries and turning the migration on without running the new build against RavenDB, which is what restamps the version.
    [Test]
    public async Task A_source_last_written_by_an_older_major_version_is_refused()
    {
        await DeleteSourceDataVersion();
        await SeedSourceDataVersion("1.0.0");

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the source is at a data version this build can read")
            .And.Contain("1.0.0")
            .And.Contain("brings the source up to date and restamps it"));
    }

    // Without this check a customer on an intermediate build fills a database they can never finish.
    [Test]
    public async Task A_build_that_cannot_copy_every_required_category_is_refused()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "NotACategory");

        var copyable = MigrationStartup.CopyableCategoryIds(await SourceSupportedCategoryIds(), Target.SupportedCategoryIds);
        var stillMissing = MigrationCategoryRegistry.All
            .Where(category => category.Kind == MigrationCategoryKind.Required && !copyable.Contains(category.Id))
            .Select(category => category.Id)
            .FirstOrDefault();

        Assert.That(stillMissing, Is.Not.Null, "this build can now copy every required category, so this test has outlived its subject and should be deleted");

        var refusal = await RefusedStartup(allowIncompleteCategorySet: false);

        Assert.That(refusal.Message, Does.Contain("this build can copy every required category")
            .And.Contain(stillMissing)
            .And.Not.Contain("the selected categories are coherent"),
            "this refusal arrives before every check that follows it, including the one the broken setting above would trip");
    }

    [Test]
    public async Task A_halted_required_category_stops_the_host_rather_than_opening_it()
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, AllowingAnIncompleteCategorySet(builder => builder.HaltTheEndpointSettingsCategory()), cancellation.Token));

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("EndpointSettings").And.Contain("Halted"));
            Assert.That(exception.Message, Does.Contain("nothing has been lost"));
            Assert.That(exception.Message, Does.Not.Contain("AllowIncompleteExit"), "the clean abort is the answer here, not the flag that accepts loss");
            Assert.That(async () => await HttpClient.GetAsync(EndpointSettingsUrl), Throws.InstanceOf<HttpRequestException>(), "Kestrel bound its socket behind a halted required category");
        });
    }

    // Every refusal is asserted against a source that has rows to copy: an empty target proves nothing otherwise.
    // Only the test about the required-set check itself withholds the marker; every other refusal has to get past it.
    async Task<Exception> RefusedStartup(bool allowIncompleteCategorySet = true)
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var refusal = Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, allowIncompleteCategorySet ? AllowingAnIncompleteCategorySet() : null, cancellation.Token));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.Message, Does.Contain("nothing has been copied"));
            Assert.That(await TargetEndpointSettingsCount(), Is.Zero, "a check that fires after rows have moved is worse than no check");
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "a refused startup has opened on nothing, so the abort is still free");
        }

        return refusal;
    }

    Task<int> TargetEndpointSettingsCount() =>
        QueryTarget(dbContext => dbContext.EndpointSettings.CountAsync());

    // The fixture stamps the source in its SetUp, so the two data version refusals start by removing that stamp.
    async Task DeleteSourceDataVersion()
    {
        using var session = SourceStore.OpenAsyncSession(Source.PrimaryDatabase);

        session.Delete(DataVersionDocumentId);

        await session.SaveChangesAsync();
    }

    static string ExpiredClientCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=sc-migration-source-expired", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }
}
