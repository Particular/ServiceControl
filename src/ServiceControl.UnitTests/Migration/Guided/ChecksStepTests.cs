namespace ServiceControl.UnitTests.Migration.Guided;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class ChecksStepTests
{
    [Test]
    public async Task Each_check_prints_ok_with_what_it_checked_against()
    {
        var harness = new GuidedMigrationHarness();
        harness.Source.Version = "6.2.4";

        await harness.Run(GuidedMigrationArguments.Unattended);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(harness.Output, Does.Match(@"(?m)^   \[ok\]   the source and target are the supported pair \(RavenDB to PostgreSQL\)\r?$"));
            Assert.That(harness.Output, Does.Match(@"(?m)^   \[ok\]   the retry history depth will not empty a migrated table \(10\)\r?$"));
            Assert.That(harness.Output, Does.Match(@"(?m)^   \[ok\]   the migration source opens \(.*6\.2\.4\)\r?$"));
        }
    }

    [Test]
    public void The_first_failure_prints_FAIL_then_skip_for_the_rest_and_stops_before_any_checkpoint_is_read()
    {
        var harness = new GuidedMigrationHarness();
        harness.Store.ReadFailure = new InvalidOperationException("The checkpoint table was read.");
        harness.Readiness.Checks.Add(new FailingCheck("the SQL schema is current", "The schema is behind."));

        Assert.That(async () => await harness.Run(GuidedMigrationArguments.Unattended),
            Throws.Exception.With.Message.Contain("Migration check 'the SQL schema is current' failed, so this run has copied nothing. The schema is behind."),
            "a target without --setup has no checkpoint table, so reading it first would hide the advice");
        Assert.That(harness.Output, Does.Match(@"(?ms)\[FAIL\] the SQL schema is current.*\[skip\] the migration target opens.*\[skip\] the migration source opens"));
    }

    sealed class FailingCheck(string name, string message) : IMigrationStartupCheck
    {
        public string Name => name;

        public Task Run(CancellationToken cancellationToken = default) => throw new Exception(message);
    }
}
