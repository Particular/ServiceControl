namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.Persistence.RavenDB.DataMigration;

class RavenDataVersionTests : RavenMigrationSourceTestBase
{
    [OneTimeSetUp]
    public static void TheVersionStringIsUsable()
    {
        Assert.That(Version.TryParse(RavenDataVersion.Current.Split('-')[0], out var version), Is.True,
            $"RavenDataVersion.Current is '{RavenDataVersion.Current}', which does not parse once the pre-release suffix is cut, so the data version check can neither pass nor refuse for the right reason.");

        Assert.That(version.Major, Is.GreaterThan(1),
            "This is a Debug build, which MinVer stamps 1.0.0, so this instance will refuse any database stamped by a release build and its own stamp will satisfy every later check. Run these tests in Release.");
    }

    [Test]
    public async Task Starting_the_persister_stamps_the_build_that_wrote_the_database()
    {
        using var session = DocumentStore.OpenAsyncSession();

        var stamp = await session.LoadAsync<RavenDataVersion>(RavenDataVersion.DocumentId, TestContext.CurrentContext.CancellationToken);

        Assert.That(stamp, Is.Not.Null, "Without a stamp a source read three years after it was last written looks identical to one this build wrote a minute ago.");
        Assert.Multiple(() =>
        {
            Assert.That(stamp.Version, Is.EqualTo(RavenDataVersion.Current));
            Assert.That(stamp.StampedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
        });
    }

    [Test]
    public async Task Starting_the_persister_stamps_the_throughput_database_as_well()
    {
        using var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName);

        var stamp = await session.LoadAsync<RavenDataVersion>(RavenDataVersion.DocumentId, TestContext.CurrentContext.CancellationToken);

        Assert.That(stamp, Is.Not.Null, "The migration reads the throughput database too, so an unstamped one is a source the version check has nothing to judge.");
        Assert.Multiple(() =>
        {
            Assert.That(stamp.Version, Is.EqualTo(RavenDataVersion.Current));
            Assert.That(stamp.StampedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
        });
    }

    [Test]
    public async Task Starting_the_persister_never_lowers_a_stamp_a_newer_build_wrote()
    {
        // This is an ordinary rollback: a newer build runs once, then an older one starts.
        await Stamp("999.0.0");

        await new DatabaseSetup((RavenPersisterSettings)PersistenceSettings, DocumentStore).Execute(TestContext.CurrentContext.CancellationToken);

        using var session = DocumentStore.OpenAsyncSession();
        var stamp = await session.LoadAsync<RavenDataVersion>(RavenDataVersion.DocumentId, TestContext.CurrentContext.CancellationToken);

        Assert.That(stamp.Version, Is.EqualTo("999.0.0"),
            "A lowered stamp makes the source check compare equal and pass on a database a newer build has written.");
    }

    [Test]
    public async Task Starting_the_persister_replaces_a_stamp_nothing_can_compare()
    {
        // The check refuses any stamp it cannot parse, so if startup left one in place no restart could get this instance past the check.
        await Stamp("not-a-version");

        await new DatabaseSetup((RavenPersisterSettings)PersistenceSettings, DocumentStore).Execute(TestContext.CurrentContext.CancellationToken);

        using var session = DocumentStore.OpenAsyncSession();
        var stamp = await session.LoadAsync<RavenDataVersion>(RavenDataVersion.DocumentId, TestContext.CurrentContext.CancellationToken);

        Assert.That(stamp.Version, Is.EqualTo(RavenDataVersion.Current));
    }

    [Test]
    public async Task The_source_check_accepts_a_database_this_build_stamped()
    {
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        Assert.DoesNotThrowAsync(() => check.Run(TestContext.CurrentContext.CancellationToken),
            "A database the running build just stamped is the one case the check has to let through.");
    }

    [Test]
    public async Task The_source_check_refuses_a_database_carrying_no_stamp()
    {
        await DeleteStamp(DatabaseName);
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        var refusal = Assert.ThrowsAsync<Exception>(() => check.Run(TestContext.CurrentContext.CancellationToken));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(DatabaseName));
            Assert.That(refusal.Message, Does.Contain(RavenDataVersion.Current));
            Assert.That(refusal.Message, Does.Contain(MigrationSettings.EnabledKey));
        });
    }

    [Test]
    public async Task The_source_check_refuses_a_database_stamped_by_a_newer_major()
    {
        await Stamp("999.0.0");
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        var refusal = Assert.ThrowsAsync<Exception>(() => check.Run(TestContext.CurrentContext.CancellationToken));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(DatabaseName));
            Assert.That(refusal.Message, Does.Contain("999.0.0"));
            Assert.That(refusal.Message, Does.Contain(RavenDataVersion.Current));
            Assert.That(refusal.Message, Does.Contain("Migrate with the newer version instead"));
        });
    }

    [Test]
    public async Task The_source_check_refuses_a_database_stamped_by_an_older_major()
    {
        await Stamp("1.0.0");
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        var refusal = Assert.ThrowsAsync<Exception>(() => check.Run(TestContext.CurrentContext.CancellationToken));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(DatabaseName));
            Assert.That(refusal.Message, Does.Contain("1.0.0"));
            Assert.That(refusal.Message, Does.Contain(RavenDataVersion.Current));
            Assert.That(refusal.Message, Does.Contain("Start this instance once on RavenDB"),
                "An upgrade that never restarted on RavenDB is the case this check exists for, so its refusal has to name the restart that fixes it.");
        });
    }

    [Test]
    public async Task The_source_check_refuses_a_database_stamped_with_something_it_cannot_compare()
    {
        await Stamp("not-a-version");
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        var refusal = Assert.ThrowsAsync<Exception>(() => check.Run(TestContext.CurrentContext.CancellationToken));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(DatabaseName));
            Assert.That(refusal.Message, Does.Contain("not-a-version"));
            Assert.That(refusal.Message, Does.Contain(RavenDataVersion.Current));
            Assert.That(refusal.Message, Does.Contain("refuses rather than guessing"),
                "Passing on a stamp it cannot read is the one outcome a version check must never produce, and it would copy the whole source unchecked.");
        });
    }

    [Test]
    public async Task The_source_check_refuses_a_database_whose_stamp_carries_no_version_at_all()
    {
        // Newtonsoft does not enforce required, so a document written without the property loads as null.
        await Stamp(null);
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        var refusal = Assert.ThrowsAsync<Exception>(() => check.Run(TestContext.CurrentContext.CancellationToken));

        Assert.That(refusal.Message, Does.Contain("refuses rather than guessing"),
            "A NullReferenceException names neither the database nor what to do about it.");
    }

    [Test]
    public async Task The_source_check_refuses_a_throughput_database_carrying_no_stamp()
    {
        await DeleteStamp(ThroughputDatabaseName);
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        var refusal = Assert.ThrowsAsync<Exception>(() => check.Run(TestContext.CurrentContext.CancellationToken));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(ThroughputDatabaseName),
                "Restarting on RavenDB is the remedy for whichever database is out of date, so the refusal has to say which one it is.");
            Assert.That(refusal.Message, Does.Contain(RavenDataVersion.Current));
            Assert.That(refusal.Message, Does.Contain(MigrationSettings.EnabledKey));
        });
    }

    [Test]
    public async Task The_source_check_refuses_a_throughput_database_stamped_by_an_older_major()
    {
        await Stamp(ThroughputDatabaseName, "1.0.0");
        await using var source = await OpenMigrationSource();
        var check = source.ContributedChecks().Single();

        var refusal = Assert.ThrowsAsync<Exception>(() => check.Run(TestContext.CurrentContext.CancellationToken));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(ThroughputDatabaseName));
            Assert.That(refusal.Message, Does.Contain("1.0.0"));
            Assert.That(refusal.Message, Does.Contain(RavenDataVersion.Current));
            Assert.That(refusal.Message, Does.Contain("Start this instance once on RavenDB"));
        });
    }

    string DatabaseName => ((RavenPersisterSettings)PersistenceSettings).DatabaseName;

    string ThroughputDatabaseName => ((RavenPersisterSettings)PersistenceSettings).ThroughputDatabaseName;

    Task Stamp(string version) => Stamp(DatabaseName, version);

    async Task Stamp(string databaseName, string version)
    {
        using var session = DocumentStore.OpenAsyncSession(databaseName);
        await session.StoreAsync(new RavenDataVersion { Version = version, StampedAt = DateTime.UtcNow }, RavenDataVersion.DocumentId, TestContext.CurrentContext.CancellationToken);
        await session.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    }

    async Task DeleteStamp(string databaseName)
    {
        using var session = DocumentStore.OpenAsyncSession(databaseName);
        session.Delete(RavenDataVersion.DocumentId);
        await session.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    }
}
