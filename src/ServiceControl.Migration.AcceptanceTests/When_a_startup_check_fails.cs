namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;

[TestFixture]
[NonParallelizable]
class When_a_startup_check_fails : MigrationAcceptanceTest
{
    [Test]
    public async Task A_target_that_already_holds_data_is_refused()
    {
        await QueryTarget(dbContext =>
        {
            dbContext.Settings.Add(new SettingEntity { Key = "NotificationEmails", Value = "{}" });
            return dbContext.SaveChangesAsync();
        });
        var settingsTable = await QueryTarget(dbContext => Task.FromResult(dbContext.Model.FindEntityType(typeof(SettingEntity))!.GetTableName()!));

        var refusal = await RefusedStartup();

        Assert.That(refusal.Message, Does.Contain("the migration target holds no ServiceControl data").And.Contain(settingsTable).And.Contain("--setup"));
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
    public async Task A_source_that_will_not_open_before_the_required_copy_settled_still_refuses()
    {
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints);
        await SeedCheckpoint(MigrationCategoryIds.EndpointSettings, MigrationCategoryState.InProgress);

        var missing = $"{Source.PrimaryDatabase}-does-not-exist";
        SetSourceVariable("SERVICECONTROL_RAVENDB_DATABASENAME", missing);

        var refusal = await RefusedStartup();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.Message, Does.Contain("the migration source opens").And.Contain(missing));
            Assert.That(refusal.Message, Does.Contain("must be restored to finish the migration").And.Not.Contain("--abandon="), "a required category cannot be finished without its source, and the run offers no abandon for a lost source");
            Assert.That(await ReadCheckpoint(MigrationCategoryIds.EventLog), Is.Null, "a refusal writes nothing");
        }
    }

    // Every refusal is asserted against a source that has rows to copy: an empty target proves nothing otherwise.
    async Task<Exception> RefusedStartup()
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var refusal = Assert.ThrowsAsync<Exception>(async () =>
            await MigrateCommand.Run(Settings, null, cancellation.Token));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.Message, Does.Contain("this run has copied nothing"));
            Assert.That(await TargetEndpointSettingsCount(), Is.Zero, "a check that fires after rows have moved is worse than no check");
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
