namespace ServiceControl.UnitTests.Migrations

{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Database;

    [TestFixture]
    public class DatabaseMigrationsTest
    {
        StringBuilder debugLog;

        [SetUp]
        public void Setup()
        {
            Debug.Listeners.Clear();
            debugLog = new StringBuilder();
            Debug.Listeners.Add(new TextWriterTraceListener(new StringWriter(debugLog)));
            Debug.Listeners.Add(new ConsoleTraceListener());
        }

        [Test]
        public void It_finishes_after_first_successful_attempt()
        {
            RunDataMigration("Return0");

            Assert.IsFalse(debugLog.ToString().Contains("Attempt 2"));
        }

        [Test]
        public void It_parses_progress_information()
        {
            var progressLog = RunDataMigration("UpdateProgress");

            CollectionAssert.AreEquivalent(new []
            {
                "Updating schema from version 1",
                "Updating schema from version 2",
                "Updating schema from version 3",
                ""
            }, progressLog);
        }

        [Test]
        public void If_first_attempt_throws_it_runs_second_attempt()
        {
            RunDataMigration("Throw", "Return0");

            Assert.IsTrue(debugLog.ToString().Contains("Attempt 1"));
            Assert.IsTrue(debugLog.ToString().Contains("Attempt 2"));
        }

        [Test]
        public void It_captures_error_stream_when_returning_non_zero()
        {
            var ex = Assert.Throws<DatabaseMigrationsException>(() =>
            {
                RunDataMigration("WriteToErrorAndExitNonZero");
            });

            StringAssert.Contains("Some error message", ex.Message);
        }

        [Test]
        public void It_ignores_error_output_when_returning_zero()
        {
            RunDataMigration("WriteToErrorAndExitZero");
        }

        [Test]
        public void It_handles_timeouts()
        {
            Assert.Throws<DatabaseMigrationsTimeoutException>(() =>
            {
                RunDataMigration(1000, "Timeout");
            });
        }

        List<string> RunDataMigration(int timeoutMilliseconds, params string[] commands)
        {
            var progressLog = new List<string>();
            var index = 0;
            DatabaseMigrations.RunDataMigration(s => { progressLog.Add(s); }, AppDomain.CurrentDomain.BaseDirectory, "DatabaseMigrationsTester.exe", timeoutMilliseconds, () =>
            {
                var nextCommand = commands[index % commands.Length];
                index++;
                return nextCommand;
            });
            return progressLog;
        }

        List<string> RunDataMigration(params string[] commands)
        {
            return RunDataMigration(10000, commands);
        }
    }
}
