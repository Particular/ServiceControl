namespace ServiceControl.UnitTests.Hosting
{
    using System;
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;
    using ServiceControl.Hosting.Commands;

    [TestFixture]
    class HostArgumentsTests
    {
        [Test]
        public void Setup_keeps_the_error_ingestion_only_flag_so_setup_can_refuse_it()
        {
            var arguments = new HostArguments(["--setup", "--error-ingestion-only"]);

            Assert.Multiple(() =>
            {
                Assert.That(arguments.Command, Is.EqualTo(typeof(SetupCommand)));
                Assert.That(arguments.ErrorIngestionOnly, Is.True);
            });
        }

        [Test]
        public void Error_ingestion_only_alone_runs_the_ingestion_only_host()
        {
            var arguments = new HostArguments(["--error-ingestion-only"]);

            Assert.That(arguments.Command, Is.EqualTo(typeof(ErrorIngestionOnlyCommand)));
        }

        [TestCase("--maintenance", typeof(MaintenanceModeCommand))]
        [TestCase("--import-failed-errors", typeof(ImportFailedErrorsCommand))]
        public void Other_modes_still_win_over_error_ingestion_only(string mode, Type expected)
        {
            var arguments = new HostArguments([mode, "--error-ingestion-only"]);

            Assert.That(arguments.Command, Is.EqualTo(expected));
        }

        [Test]
        public void Setup_alone_is_not_error_ingestion_only()
        {
            var arguments = new HostArguments(["--setup"]);

            Assert.Multiple(() =>
            {
                Assert.That(arguments.Command, Is.EqualTo(typeof(SetupCommand)));
                Assert.That(arguments.ErrorIngestionOnly, Is.False);
            });
        }
    }
}
