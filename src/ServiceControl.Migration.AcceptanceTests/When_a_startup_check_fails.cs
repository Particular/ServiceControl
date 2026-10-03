namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.IO;
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
using ServiceControl.Persistence.EFCore.Abstractions;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_a_startup_check_fails : MigrationAcceptanceTest
{
    const string HostOpenedSetting = "Migration/HostOpenedOnTarget";

    [Test]
    public async Task An_optional_category_window_that_is_not_a_time_span_is_refused()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", "a week");

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the optional category windows are valid").And.Contain("a week"));
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

    // Without this check a build that copies the required set commits a customer to SQL before the rest of the migration exists.
    [Test]
    public async Task A_build_without_the_whole_migration_is_refused()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", "a week");

        var refusal = await RefusedStartup(allowUnreleasedMigration: false);

        Assert.That(refusal.Message, Does.Contain("this build carries the whole migration")
            .And.Not.Contain("the optional category windows are valid"),
            "this refusal comes first, so no other check is consulted on an unreleased build");
    }

    [Test]
    public async Task A_halted_required_category_stops_the_host_rather_than_opening_it()
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(builder => builder.HaltTheEndpointSettingsCategory()), cancellation.Token));

        Assert.Multiple(() =>
        {
            Assert.That(exception.Message, Does.Contain("EndpointSettings").And.Contain("Halted"));
            Assert.That(exception.Message, Does.Contain("nothing has been lost"));
            Assert.That(exception.Message, Does.Not.Contain("AllowIncompleteExit"), "the clean abort is the answer here, not the flag that accepts loss");
            Assert.That(async () => await HttpClient.GetAsync(EndpointSettingsUrl), Throws.InstanceOf<HttpRequestException>(), "Kestrel bound its socket behind a halted required category");
        });
    }

    // Every refusal is asserted against a source that has rows to copy: an empty target proves nothing otherwise.
    // Only the release gate's own test withholds the marker; every other refusal has to get past it.
    async Task<Exception> RefusedStartup(bool allowUnreleasedMigration = true)
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var refusal = Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, allowUnreleasedMigration ? AllowingAnUnreleasedMigration() : null, cancellation.Token));

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

    static string ExpiredClientCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=sc-migration-source-expired", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pkcs12));
    }
}
