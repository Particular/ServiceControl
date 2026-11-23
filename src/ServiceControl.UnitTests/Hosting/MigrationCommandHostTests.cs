namespace ServiceControl.UnitTests.Hosting;

using System;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationCommandHostTests
{
    [Test]
    public void A_wrapped_failure_prints_the_cause_after_the_message_each_with_its_type()
    {
        var failure = new InvalidOperationException("An exception has been raised that is likely due to a transient failure. See the inner exception.", new TimeoutException("The database did not answer within 30 seconds."));

        Assert.That(MigrationCommandHost.ErrorText(failure, "--start-data-migration").Split(Environment.NewLine), Is.EqualTo(new[]
        {
            "InvalidOperationException: An exception has been raised that is likely due to a transient failure. See the inner exception.",
            "TimeoutException: The database did not answer within 30 seconds."
        }));
    }

    [Test]
    public void A_cause_the_message_above_already_quotes_is_not_printed_again()
    {
        var server = new InvalidOperationException("Unable to start the RavenDB Server. Cannot open database because RavenDB was unable create file lock.");
        var source = new InvalidOperationException($"The RavenDB migration source could not start a server: {server.Message}", server);
        var check = new Exception($"Migration check 'the migration source opens' failed, so this run has copied nothing. {source.Message}", source);

        Assert.That(MigrationCommandHost.ErrorText(check, "--start-data-migration").Split(Environment.NewLine), Is.EqualTo(new[]
        {
            $"Exception: {check.Message}"
        }));
    }

    [Test]
    public void A_conflict_a_wrapper_quotes_still_gets_the_advice()
    {
        var conflict = new MigrationCheckpointConflictException("Checkpoint KnownEndpoints was saved from version 3, which the stored row no longer holds.");
        var wrapper = new Exception($"The required copy stopped because another writer saved a migration checkpoint. {conflict.Message}", conflict);

        Assert.That(MigrationCommandHost.ErrorText(wrapper, "--start-data-migration").Split(Environment.NewLine), Is.EqualTo(new[]
        {
            $"Exception: {wrapper.Message}",
            "ServiceControl or a migration is still running on this database. Stop it and run --start-data-migration again."
        }));
    }
}
