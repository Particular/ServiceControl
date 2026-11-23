namespace ServiceControl.UnitTests.Hosting;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using Particular.ServiceControl.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Hosting.Commands;
using ServiceControl.Infrastructure;

[TestFixture]
[NonParallelizable]
class MigrateCommandLogFileTests
{
    const string LogFile = "migration-2026-11-19-101003.txt";

    [TestCase("--start-data-migration", true)]
    [TestCase("--start-data-migration --error-ingestion-only", false)]
    [TestCase("--migration-status", false)]
    [TestCase("", false)]
    public void Only_a_migration_run_logs_to_its_own_file(string arguments, bool expected) =>
        Assert.That(MigrateCommand.LogsToItsOwnFile(new HostArguments(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))), Is.EqualTo(expected));

    [Test]
    public async Task A_failed_run_logs_its_stack_and_names_the_log_file_last()
    {
        var settings = new Settings(persisterType: "NoSuchPersister");
        var logged = new MemoryTarget("logged") { Layout = "${level}|${message}|${exception:format=tostring}" };
        var error = new StringWriter();
        var previousError = Console.Error;

        var output = await Capture(async cancellationToken =>
        {
            Console.SetError(error);

            try
            {
                // A logger made before any Settings may have no NLog provider, and the static loggers keep the first one made.
                LoggerUtil.DisposeLoggerFactories();
                await new MigrateCommand().Execute(new HostArguments(["--start-data-migration", "--non-interactive"]), settings, cancellationToken);
            }
            finally
            {
                Console.SetError(previousError);
            }
        }, logged);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.ToString(), Does.Contain("NoSuchPersister"));
            Assert.That(output, Does.EndWith(LogLine), "the log line comes last");
            Assert.That(output.Split(LogLine), Has.Length.EqualTo(2), "the log line is printed once");
            Assert.That(error.ToString(), Does.Not.Contain("The log for this run is"));
            Assert.That(logged.Logs, Has.One.Match(@"^Error\|The offline migration stopped on an error\|[\s\S]*NoSuchPersister[\s\S]*\n\s+at "),
                "the console shows no stack, and outside a container this log is the only place it is kept");
        }
    }

    [Test]
    public async Task Finish_names_the_log_file_only_when_the_run_has_one()
    {
        var withFile = await Capture(cancellationToken => MigrateCommand.Finish(Task.FromResult(1), cancellationToken));
        var exitCode = Environment.ExitCode;
        var withoutFile = await Capture(cancellationToken => MigrateCommand.Finish(Task.FromResult(0), cancellationToken), withFile: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(withFile, Is.EqualTo($"{Environment.NewLine}{LogLine}"), "Finish adds only a blank line and the log line");
            Assert.That(withoutFile, Is.Empty, "a container run has no log file, so there is nothing to name");
        }
    }

    static readonly string LogLine = $"The log for this run is {Path.Combine(Path.GetTempPath(), LogFile)}.{Environment.NewLine}";

    static async Task<string> Capture(Func<CancellationToken, Task> act, Target logged = null, bool withFile = true)
    {
        var previous = LogManager.Configuration;
        var previousOut = Console.Out;
        var output = new StringWriter();

        try
        {
            var configuration = new LoggingConfiguration();
            if (withFile)
            {
                configuration.AddTarget(new FileTarget { Name = "file", FileName = Path.Combine(Path.GetTempPath(), LogFile) });
            }

            if (logged is not null)
            {
                configuration.AddRuleForAllLevels(logged);
            }

            LogManager.Configuration = configuration;
            Console.SetOut(output);

            await act(CancellationToken.None);
        }
        finally
        {
            Console.SetOut(previousOut);
            LogManager.Configuration = previous;
        }

        return output.ToString();
    }

    [TearDown]
    public void ResetExitCode() => Environment.ExitCode = 0;
}
