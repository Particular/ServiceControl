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
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;
using ServiceControl.Persistence.EFCore.Infrastructure;

class MigrationTargetReadinessTests : PersistenceTestBase
{
    IMigrationTargetReadiness Readiness => ServiceProvider.GetRequiredService<IMigrationTargetReadiness>();

    [Test]
    public void The_target_contributes_the_three_checks_only_it_can_make() =>
        Assert.That(
            Readiness.ContributedChecks().Select(check => check.GetType()),
            Is.EqualTo(new[] { typeof(RetryHistoryDepthIsSafeCheck), typeof(SchemaIsCurrentCheck), typeof(BodyStorageIsWritableCheck) }));

    [Test]
    public void Every_contributed_check_passes_against_a_migrated_database()
    {
        // PersistenceFactory.Create is what carries the host's depth onto the persister, and the test container never runs it.
        PersistenceSettings.RetryHistoryDepth = 10;

        using (Assert.EnterMultipleScope())
        {
            foreach (var check in Readiness.ContributedChecks())
            {
                Assert.DoesNotThrowAsync(() => check.Run(), $"a healthy target must pass every contributed check, and it failed '{check.Name}'");
            }
        }
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

    [Test]
    public void The_retry_history_depth_check_passes_at_the_default_depth() =>
        Assert.DoesNotThrowAsync(() => new RetryHistoryDepthIsSafeCheck(10).Run());

    [Test]
    public void The_retry_history_depth_check_refuses_a_depth_that_empties_the_table()
    {
        var exception = Assert.ThrowsAsync<Exception>(() => new RetryHistoryDepthIsSafeCheck(0).Run());

        Assert.That(exception.Message, Does.Contain("RetryHistoryDepth").And.Contain("HistoricRetryOperations"));
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
