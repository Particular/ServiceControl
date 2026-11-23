namespace ServiceControl.UnitTests.Hosting;

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ServiceControl.Hosting;
using ServiceControl.Hosting.Commands;
using ServiceControl.Migration.Guided;

[TestFixture]
[NonParallelizable]
class GuidedMigrationArgumentsTests
{
    [Test]
    public void Every_guided_argument_parses_alongside_start_data_migration()
    {
        var arguments = new HostArguments(["--start-data-migration", "--non-interactive",
            "--abandon=UnresolvedAndRetryIssuedFailedMessages", "--abandon=KnownEndpoints", "--retry=EndpointSettings"]);

        var parsed = GuidedMigrationArguments.Parse(arguments.NonInteractive, arguments.Abandon, arguments.Retry);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(arguments.Command, Is.EqualTo(typeof(MigrateCommand)));
            Assert.That(parsed.NonInteractive, Is.True);
            Assert.That(parsed.Abandon, Is.EquivalentTo(new[] { "UnresolvedAndRetryIssuedFailedMessages", "KnownEndpoints" }));
            Assert.That(parsed.Retry, Is.EquivalentTo(new[] { "EndpointSettings" }));
        }
    }

    [TestCase("NoSuchCategory")]
    public void Abandon_and_retry_take_only_a_required_category(string categoryId)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => GuidedMigrationArguments.Parse(false, [categoryId], []), Throws.ArgumentException.With.Message.Contain($"--abandon={categoryId}"));
            Assert.That(() => GuidedMigrationArguments.Parse(false, [], [categoryId]), Throws.ArgumentException.With.Message.Contain($"--retry={categoryId}"));
        }
    }

    [Test]
    public async Task A_bad_argument_stops_the_command_before_anything_is_built()
    {
        var originalOut = Console.Out;
        var originalExitCode = Environment.ExitCode;
        var output = new StringWriter();
        Console.SetOut(output);

        try
        {
            await new MigrateCommand().Execute(new HostArguments(["--start-data-migration", "--abandon=EventLog"]), settings: null);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Environment.ExitCode, Is.EqualTo(1));
                Assert.That(output.ToString(), Does.Contain("--abandon=EventLog is not a required migration category."));
            }
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.ExitCode = originalExitCode;
        }
    }
}
