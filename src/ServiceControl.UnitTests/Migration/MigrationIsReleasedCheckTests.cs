namespace ServiceControl.UnitTests.Migration;

using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationIsReleasedCheckTests
{
    [Test]
    public void A_build_without_the_whole_migration_refuses_MigrationEnabled()
    {
        var exception = Assert.ThrowsAsync<Exception>(() => new MigrationIsReleasedCheck().Run());

        Assert.That(exception.Message, Does.Contain("does not yet carry the whole migration").And.Contain(MigrationSettings.EnabledKey),
            "the customer has to learn that nothing will be copied, and which setting to turn back off");
    }

    // A RavenDB target fails the pair check, so this only passes while the release check runs before it.
    [Test]
    public void An_unreleased_build_refuses_before_any_other_check()
    {
        var settings = new Settings(transportType: "LearningTransport", persisterType: "RavenDB", errorRetentionPeriod: TimeSpan.FromDays(10));
        using var services = new ServiceCollection().BuildServiceProvider();

        var exception = Assert.ThrowsAsync<Exception>(() => MigrationStartup.RunRequiredCopy(services, settings));

        Assert.That(exception.Message, Does.Contain("this build carries the whole migration").And.Not.Contain("supported pair"));
    }

    [Test]
    public void The_test_marker_lets_a_copy_run_on_an_unreleased_build()
    {
        Assert.DoesNotThrowAsync(() => new MigrationIsReleasedCheck(new AllowUnreleasedMigration()).Run());
    }
}
