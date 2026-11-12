namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;
using ServiceControl.Persistence.EFCore.Infrastructure;

class MigrationTargetReadinessTests : PersistenceTestBase
{
    IMigrationTargetReadiness Readiness => ServiceProvider.GetRequiredService<IMigrationTargetReadiness>();

    [Test]
    public void The_target_contributes_the_three_checks_only_it_can_make() =>
        Assert.That(
            Readiness.ContributedChecks().Select(check => check.GetType()),
            Is.EqualTo(new[] { typeof(SchemaIsCurrentCheck), typeof(TargetHoldsNoServiceControlDataCheck), typeof(BodyStorageIsWritableCheck) }));

    [Test]
    public void Every_contributed_check_passes_against_a_migrated_database()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var check in Readiness.ContributedChecks())
            {
                Assert.DoesNotThrowAsync(() => check.Run(), $"a healthy target must pass every contributed check, and it failed '{check.Name}'");
            }
        }
    }

    [Test]
    public async Task A_target_holding_a_row_is_refused_while_no_checkpoint_exists()
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true });

        var exception = Assert.ThrowsAsync<Exception>(() => TargetCheck.Run());

        Assert.That(exception.Message, Does.Contain(TableName<EndpointSettingsEntity>()).And.Contain("--setup"));
    }

    // A plain start writes this row first, so a key someone later ignores fails here before it reaches a customer.
    [Test]
    public async Task A_settings_row_counts_as_data()
    {
        await ServiceProvider.GetRequiredService<ITrialLicenseDataProvider>().StoreTrialEndDate(new DateOnly(2030, 1, 1));

        var exception = Assert.ThrowsAsync<Exception>(() => TargetCheck.Run());

        Assert.That(exception.Message, Does.Contain(TableName<SettingEntity>()));
    }

    [Test]
    public async Task A_target_with_a_required_checkpoint_row_is_not_judged()
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true });
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.Complete);

        Assert.DoesNotThrowAsync(() => TargetCheck.Run());
    }

    [Test]
    public async Task A_row_under_an_id_this_build_does_not_know_counts_as_a_started_copy()
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true });
        await SeedCheckpoint("SomeCategoryFromANewerBuild", MigrationCategoryState.InProgress);

        Assert.DoesNotThrowAsync(() => TargetCheck.Run());
    }

    // --migration-abandon writes this row before any copy has run, so on a database that already served on SQL it must not switch the check off.
    [Test]
    public async Task An_abandoned_optional_row_alone_does_not_skip_the_check()
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true });
        await SeedCheckpoint(MigrationCategoryIds.EventLog, MigrationCategoryState.Abandoned);

        var exception = Assert.ThrowsAsync<Exception>(() => TargetCheck.Run());

        Assert.That(exception.Message, Does.Contain(TableName<EndpointSettingsEntity>()));
    }

    [Test]
    public void Every_mapped_entity_but_the_checkpoint_is_judged()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var mapped = dbContext.Model.GetEntityTypes().Select(type => type.ClrType).Where(type => type != typeof(MigrationCheckpointEntity));

        Assert.That(TargetHoldsNoServiceControlDataCheck.Tables.Select(entry => entry.Entity), Is.EquivalentTo(mapped));
    }

    IMigrationStartupCheck TargetCheck => Readiness.ContributedChecks().OfType<TargetHoldsNoServiceControlDataCheck>().Single();

    Task SeedCheckpoint(string categoryId, MigrationCategoryState state) =>
        ServiceProvider.GetRequiredService<IMigrationCheckpointStore>().Upsert(new MigrationCheckpoint(categoryId, state, null, 0, 0, null, null, null, null, null, null));

    string TableName<T>()
    {
        using var scope = ServiceProvider.CreateScope();

        return scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model.FindEntityType(typeof(T))!.GetTableName()!;
    }

    [Test]
    public async Task The_schema_check_refuses_a_database_whose_migrations_have_not_been_applied()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        // EF's own history repository, because it is the only thing that knows where the history table is once the persister is given a schema.
        var history = dbContext.GetService<IHistoryRepository>();

        foreach (var row in await history.GetAppliedMigrationsAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(history.GetDeleteScript(row.MigrationId));
        }

        var exception = Assert.ThrowsAsync<Exception>(() => Readiness.ContributedChecks().OfType<SchemaIsCurrentCheck>().Single().Run());

        Assert.That(exception.Message, Does.Contain(dbContext.Database.GetMigrations().First()).And.Contain("--setup"));
    }

    [Test]
    public async Task The_body_storage_check_leaves_no_probe_body_behind()
    {
        var bodyStorage = ServiceProvider.GetRequiredService<IBodyStoragePersistence>();

        await Readiness.ContributedChecks().Single(check => check.Name == "message body storage is writable").Run();

        Assert.That(await bodyStorage.ReadBody("migration-writable-probe"), Is.Null, "a probe left in the store is a body the source never had, which a count comparison reads as an extra rather than a loss");
    }

    [Test]
    public void The_body_storage_check_refuses_a_store_that_cannot_write()
    {
        var parentThatIsAFile = Path.Combine(Path.GetTempPath(), $"sc-not-a-directory-{Guid.NewGuid():n}");
        File.WriteAllText(parentThatIsAFile, string.Empty);

        try
        {
            var check = new BodyStorageIsWritableCheck(new FileSystemBodyStoragePersistence(
                new FileSystemBodyStorageSettings { StoragePath = Path.Combine(parentThatIsAFile, "bodies") }));

            Assert.CatchAsync<IOException>(() => check.Run());
        }
        finally
        {
            File.Delete(parentThatIsAFile);
        }
    }

    // The copy runs before any hosted service starts, so a target that only works once one has started is broken exactly when the migration needs it.
    [Test]
    public async Task The_target_answers_from_a_container_whose_hosted_services_have_not_started()
    {
        var (host, context) = await BuildHostWithoutStarting();

        try
        {
            await using var scope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

            Assert.That(await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().EndpointSettings.LongCountAsync(), Is.Zero);
            Assert.DoesNotThrowAsync(() => host.Services.GetRequiredService<IMigrationTargetReadiness>().ContributedChecks().Single(check => check.Name == "message body storage is writable").Run());
        }
        finally
        {
            await context.TearDown();
            host.Dispose();
        }
    }

    // PersistenceTestBase already started a host in SetUp, so this builds a second one over its own test database.
    static async Task<(IHost Host, PersistenceTestsContext Context)> BuildHostWithoutStarting()
    {
        var context = new PersistenceTestsContext();
        var hostBuilder = Host.CreateApplicationBuilder();

        await context.Setup(hostBuilder);
        var host = hostBuilder.Build();
        await context.InstallSchema(host);

        return (host, context);
    }

    [Test]
    public async Task The_host_opened_record_is_absent_until_written_and_stays_after_a_second_write()
    {
        var readiness = ServiceProvider.GetRequiredService<IMigrationTargetReadiness>();

        Assert.That(await readiness.HasHostOpened(), Is.False, "a target nothing has opened on must not claim otherwise");

        await readiness.RecordHostOpened();
        var firstOpened = Now;

        AdvanceClock(TimeSpan.FromDays(7));
        await readiness.RecordHostOpened();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await readiness.HasHostOpened(), Is.True);
            // The stored instant is what tells an operator the clean abort is over, so a later start must not move it forward.
            Assert.That(await ReadHostOpenedAt(), Is.EqualTo(firstOpened));
        }
    }

    async Task<DateTime?> ReadHostOpenedAt()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.GetSetting<DateTime?>(SettingKeys.MigrationHostOpenedOnTarget);
    }
}
