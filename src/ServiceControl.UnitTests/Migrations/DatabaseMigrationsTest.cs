namespace ServiceControl.UnitTests.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using NUnit.Framework;
    using Raven.Database.FileSystem.Storage.Voron.Impl;
    using ServiceControlInstaller.Engine.Database;

    [TestFixture]
    public class DatabaseMigrationsTest
    {
        StringBuilder traceLog;

        [SetUp]
        public void Setup()
        {
            traceLog = new StringBuilder();
            Trace.AutoFlush = true;
            Trace.Listeners.Remove("DatabaseMigrationsTest");
            Trace.Listeners.Add(new TextWriterTraceListener(new StringWriter(traceLog), "DatabaseMigrationsTest"));
        }

        [Test]
        public void It_finishes_after_first_successful_attempt()
        {
            RunDataMigration("Return0");

            StringAssert.DoesNotContain("Attempt 2", traceLog.ToString());
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
                "OK Upgrading",
                "OK"
            }, progressLog);
        }

        [Test]
        public void If_first_attempt_throws_it_runs_second_attempt()
        {
            RunDataMigration("Throw", "Return0");

            var actual = traceLog.ToString();
            StringAssert.Contains("Attempt 1", actual);
            StringAssert.Contains("Attempt 2", actual);
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

<<<<<<< HEAD
        List<string> RunDataMigration(params string[] commands)
=======
        [Test]
        public void It_handles_timeouts()
        {
            Assert.Throws<DatabaseMigrationsTimeoutException>(() =>
            {
                RunDataMigration(1000, "Timeout");
            });
        }
        
        [Test]
        public void It_writes_upgrade_file()
        {
            var files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.upgrade");
            foreach (var file in files)
            {
                File.Delete(file);
            }
            
            var now = DateTime.UtcNow;
            RunDataMigration("Return0");

            files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.upgrade");
            
            Assert.That(files, Has.Length.EqualTo(1));
            var fileTime = DateTime.FromFileTimeUtc(Convert.ToInt64(Path.GetFileNameWithoutExtension(files[0])));
            Assert.That(fileTime, Is.GreaterThanOrEqualTo(now).And.LessThanOrEqualTo(now.AddSeconds(10)));
        }

        List<string> RunDataMigration(int timeoutMilliseconds, params string[] commands)
>>>>>>> Spike for indexing in progress after upgrade
        {
            var progressLog = new List<string>();
            var index = 0;
            DatabaseMigrations.RunDataMigration(s => { progressLog.Add(s); }, AppDomain.CurrentDomain.BaseDirectory, "DatabaseMigrationsTester.exe", () =>
            {
                var nextCommand = commands[index % commands.Length];
                index++;
                return nextCommand;
            });
            return progressLog;
        }
    }
}
